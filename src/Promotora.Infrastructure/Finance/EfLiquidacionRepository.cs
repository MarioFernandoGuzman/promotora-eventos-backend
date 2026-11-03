using Microsoft.EntityFrameworkCore;
using Promotora.Application.Finance;
using Promotora.Application.Finance.Ports;
using Promotora.Infrastructure.Finance.Persistence;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Finance;

public sealed class EfLiquidacionRepository(PromotoraDbContext db) : ILiquidacionRepository
{
    public async Task<LiquidacionDto?> FindAsync(int cuentaId, string referencia, CancellationToken ct)
    {
        ExigirTransaccion();
        return await (from m in db.MovimientosFinancieros.AsNoTracking()
                      join c in db.CuentasFinancieras on m.CuentaId equals c.Id
                      where m.CuentaId == cuentaId && m.Tipo == "LIQUIDACION" && m.Descripcion == referencia
                      select new LiquidacionDto(m.Id, m.CuentaId, -m.Monto, c.Moneda, m.Fecha)).SingleOrDefaultAsync(ct);
    }
    public async Task<decimal> SumaMovimientosAsync(int cuentaId, CancellationToken ct)
    {
        ExigirTransaccion();
        return await db.MovimientosFinancieros.Where(m => m.CuentaId == cuentaId).SumAsync(m => (decimal?)m.Monto, ct) ?? 0m;
    }
    public async Task<LiquidacionDto?> RegistrarAsync(int cuentaId, decimal monto, string referencia, DateTime fecha, CancellationToken ct)
    {
        ExigirTransaccion();
        if (fecha.Kind != DateTimeKind.Utc || monto <= 0m || decimal.Round(monto, 2) != monto)
            throw new ArgumentException("La liquidación requiere fecha UTC y monto positivo con dos decimales.");
        int filas = await db.CuentasFinancieras.Where(c => c.Id == cuentaId && c.Tipo == "DUENO" && c.Estado == "ACTIVA" && c.Saldo >= monto)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Saldo, c => c.Saldo - monto), ct);
        if (filas != 1) return null;
        var movimiento = new MovimientoFinancieroRow { CuentaId = cuentaId, CompraId = null, Tipo = "LIQUIDACION", Monto = -monto, Descripcion = referencia, Fecha = fecha };
        db.MovimientosFinancieros.Add(movimiento); await db.SaveChangesAsync(ct);
        // PostgreSQL persiste microsegundos: devolver la fecha guardada también en el primer intento.
        return await FindAsync(cuentaId, referencia, ct)
            ?? throw new InvalidOperationException("El movimiento insertado no está disponible en su transacción.");
    }
    private void ExigirTransaccion()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("La liquidación requiere una transacción explícita y bloqueo previo de la cuenta.");
    }
}
