using Microsoft.EntityFrameworkCore;
using Promotora.Application.Finance.Ports;
using Promotora.Domain.Finance;
using Promotora.Domain.Shared;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Finance;

public sealed class EfReembolsoRepository(PromotoraDbContext db) : IReembolsoRepository
{
    public Task<IReadOnlyList<PagoReembolso>> PagosAsync(int compraId, CancellationToken ct)
    {
        ExigirTransaccion(); return LeerPagosAsync(compraId, ct);
    }
    private async Task<IReadOnlyList<PagoReembolso>> LeerPagosAsync(int compraId, CancellationToken ct)
    {
        if (await db.Pagos.AnyAsync(p => p.CompraId == compraId && p.Estado == "PENDIENTE", ct)) return [];
        return await db.Pagos.Where(p => p.CompraId == compraId && (p.Estado == "APROBADO" || p.Estado == "REEMBOLSADO"))
            .Select(p => new PagoReembolso(p.Id, p.Estado, p.Referencia, p.Monto)).ToArrayAsync(ct);
    }
    public async Task<IReadOnlyList<MovimientoOriginal>> LedgerAsync(int compraId, CancellationToken ct)
    {
        ExigirTransaccion();
        return await db.MovimientosFinancieros.Where(m => m.CompraId == compraId).OrderBy(m => m.Id)
            .Select(m => new MovimientoOriginal(m.Id, m.CuentaId, m.Tipo, m.Monto)).ToArrayAsync(ct);
    }
    public async Task<IReadOnlyList<CuentaCobro>> LockCuentasAsync(IReadOnlyList<int> ids, CancellationToken ct)
    {
        ExigirTransaccion(); var array = ids.ToArray();
        var cuentas = await db.CuentasFinancieras.FromSqlInterpolated($"SELECT * FROM cuentas_financieras WHERE id=ANY({array}) ORDER BY id FOR UPDATE").AsNoTracking().ToArrayAsync(ct);
        return cuentas.Select(c => new CuentaCobro(c.Id, c.Tipo, c.DuenoId, c.Moneda, c.Saldo, c.Estado)).ToArray();
    }
    public async Task<bool> BoletosCompletosAsync(int compraId, bool reembolsada, CancellationToken ct)
    {
        ExigirTransaccion();
        var detalles = await db.DetalleCompra.Where(d => d.CompraId == compraId)
            .Select(d => new { d.Cantidad, Emitidos = db.Boletos.Count(b => b.DetalleCompraId == d.Id), Validos = db.Boletos.Count(b => b.DetalleCompraId == d.Id && b.Estado == "VALIDO") }).ToArrayAsync(ct);
        return detalles.Length > 0 && detalles.All(d => d.Emitidos == d.Cantidad && (!reembolsada || d.Validos == 0));
    }
    public async Task<bool> MarcarPagoAsync(long pagoId, CancellationToken ct)
    {
        ExigirTransaccion();
        return await db.Pagos.Where(p => p.Id == pagoId && p.Estado == "APROBADO").ExecuteUpdateAsync(s => s.SetProperty(p => p.Estado, "REEMBOLSADO"), ct) == 1;
    }
    public async Task<Result<IReadOnlyList<BoletoAnulado>>> AnularYDevolverAsync(int compraId, DateTime ahora, CancellationToken ct)
    {
        ExigirTransaccion();
        var anulados = await db.Database.SqlQuery<BoletoAnulado>($"""
            UPDATE public.boletos b SET estado='ANULADO' FROM public.detalle_compra d
            WHERE b.detalle_compra_id=d.id AND d.compra_id={compraId} AND b.estado='VALIDO'
            RETURNING b.id, d.tipo_entrada_id
            """).ToArrayAsync(ct);
        foreach (var grupo in anulados.GroupBy(b => b.TipoEntradaId).OrderBy(g => g.Key))
        {
            int cantidad = grupo.Count();
            int filas = await db.TiposEntrada.Where(t => t.Id == grupo.Key && t.CupoDisponible <= t.CupoTotal - cantidad)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.CupoDisponible, t => t.CupoDisponible + cantidad), ct);
            if (filas != 1) return Result<IReadOnlyList<BoletoAnulado>>.Failure(new Error("reembolso.cupo", "El cupo no admite la devolución; requiere conciliación.", ErrorType.Conflict));
        }
        await Promotora.Infrastructure.Ticketing.UbicacionesNumeradas.LiberarAsync(db, compraId, anulados.Select(b => b.Id).ToArray(), ahora, ct);
        return Result<IReadOnlyList<BoletoAnulado>>.Success(anulados);
    }
    private void ExigirTransaccion()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Reembolsar requiere la transacción de compra.");
    }
}
