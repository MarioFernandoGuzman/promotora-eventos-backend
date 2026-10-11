using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Audit;
using Promotora.Application.Finance.Ports;
using Promotora.Domain.Finance;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Finance;

public sealed class EfCuentaRepository(PromotoraDbContext db, IAuditLogger audit) : ICuentaRepository, IAltaCuentaDuenoPort
{
    public async Task CrearEnTransaccionAsync(int duenoId, int? actorId, string? ip, CancellationToken ct)
    {
        ExigirTransaccion();
        // El catálogo/precios del proyecto usa GTQ. La unicidad ya está en V5/V9.
        var filas = await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO cuentas_financieras (tipo, dueno_id, moneda, saldo, estado) VALUES ('DUENO', {duenoId}, 'GTQ', 0, 'ACTIVA') ON CONFLICT (dueno_id, moneda) DO NOTHING", ct);
        if (filas == 1)
        {
            int id = await db.CuentasFinancieras.Where(c => c.DuenoId == duenoId && c.Moneda == "GTQ").Select(c => c.Id).SingleAsync(ct);
            await audit.WriteAsync(actorId, "CREATE", "cuentas_financieras", id.ToString(CultureInfo.InvariantCulture), ip, ct);
        }
    }

    public Task<int?> DuenoIdAsync(int usuarioId, CancellationToken ct) => db.DuenosRecinto.Where(d => d.UsuarioId == usuarioId).Select(d => (int?)d.Id).SingleOrDefaultAsync(ct);
    public async Task<CuentaFinanciera?> LockAsync(int id, CancellationToken ct)
    {
        ExigirTransaccion();
        var rows = await db.CuentasFinancieras.FromSqlInterpolated($"SELECT * FROM cuentas_financieras WHERE id={id} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        var c = rows.SingleOrDefault();
        return c is null ? null : new(c.Id, c.Tipo, c.DuenoId, c.Moneda, c.Saldo, c.Estado, c.FechaCierre);
    }
    public async Task<bool> CerrarAsync(int id, DateTime fecha, CancellationToken ct)
    {
        ExigirTransaccion();
        if (fecha.Kind != DateTimeKind.Utc) throw new ArgumentException("La fecha debe ser UTC.", nameof(fecha));
        return await db.CuentasFinancieras.Where(c => c.Id == id && c.Estado == "ACTIVA" && c.Tipo == "DUENO" && c.Saldo == 0m)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, "CERRADA").SetProperty(c => c.FechaCierre, fecha), ct) == 1;
    }
    private void ExigirTransaccion()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Las cuentas requieren transacción explícita.");
    }
}
