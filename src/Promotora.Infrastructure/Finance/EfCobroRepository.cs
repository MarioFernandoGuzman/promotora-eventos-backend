using Microsoft.EntityFrameworkCore;
using Promotora.Application.Finance.Ports;
using Promotora.Domain.Finance;
using Promotora.Domain.Shared;
using Promotora.Domain.Ticketing;
using Promotora.Infrastructure.Finance.Persistence;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Finance;

public sealed class EfCobroRepository(PromotoraDbContext db) : ICobroRepository
{
    public async Task<Result<CompraParaCobro>> LockCompraAsync(Guid publicId, CancellationToken ct)
    {
        ExigirTransaccion();
        var compra = (await db.Compras.FromSqlInterpolated($"SELECT * FROM compras WHERE public_id={publicId} FOR UPDATE").AsNoTracking().ToListAsync(ct)).SingleOrDefault();
        if (compra is null) return Result<CompraParaCobro>.Failure(new Error("finanzas.compra", "Compra no encontrada.", ErrorType.NotFound));
        var importes = ImportesCompra.DesdeMontos(compra.Subtotal, compra.ComisionPlataforma, compra.Impuestos, compra.Total);
        if (importes.IsFailure) return Result<CompraParaCobro>.Failure(importes.Errors);
        int dueno = await (from e in db.Eventos
                           join r in db.Recintos on e.RecintoId equals r.Id
                           where e.Id == compra.EventoId
                           select r.DuenoId).SingleAsync(ct);
        return Result<CompraParaCobro>.Success(new(compra.Id, compra.PublicId, dueno, compra.Moneda, compra.Estado, importes.Value));
    }

    public async Task<IReadOnlyList<CuentaCobro>> LockCuentasAsync(int duenoId, string moneda, CancellationToken ct)
    {
        ExigirTransaccion();
        var cuentas = await db.CuentasFinancieras.FromSqlInterpolated($"SELECT * FROM cuentas_financieras WHERE moneda={moneda} AND (tipo IN ('PLATAFORMA','IMPUESTOS') OR (tipo='DUENO' AND dueno_id={duenoId})) ORDER BY id FOR UPDATE")
            .AsNoTracking().ToArrayAsync(ct);
        return cuentas.Select(c => new CuentaCobro(c.Id, c.Tipo, c.DuenoId, c.Moneda, c.Saldo, c.Estado)).ToArray();
    }

    public Task<bool> TieneMovimientosAsync(int compraId, CancellationToken ct)
    {
        ExigirTransaccion(); return db.MovimientosFinancieros.AnyAsync(m => m.CompraId == compraId, ct);
    }

    public async Task<bool> CuentasConciliadasAsync(IReadOnlyList<int> ids, CancellationToken ct)
    {
        ExigirTransaccion();
        return !await db.CuentasFinancieras.Where(c => ids.Contains(c.Id)).AnyAsync(c => c.Saldo !=
            (db.MovimientosFinancieros.Where(m => m.CuentaId == c.Id).Sum(m => (decimal?)m.Monto) ?? 0m), ct);
    }

    public async Task<IReadOnlyList<long>> RegistrarMovimientosAsync(int compraId, IReadOnlyList<MovimientoCobro> movimientos, DateTime fecha, CancellationToken ct)
    {
        ExigirTransaccion();
        if (fecha.Kind != DateTimeKind.Utc) throw new ArgumentException("La fecha debe ser UTC.", nameof(fecha));
        var rows = new List<MovimientoFinancieroRow>();
        foreach (var movimiento in movimientos.OrderBy(m => m.CuentaId))
        {
            int filas = await db.CuentasFinancieras.Where(c => c.Id == movimiento.CuentaId && c.Estado == "ACTIVA")
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Saldo, c => c.Saldo + movimiento.Monto), ct);
            if (filas != 1) throw new InvalidOperationException("La cuenta bloqueada ya no admite el movimiento.");
            rows.Add(new MovimientoFinancieroRow { CompraId = compraId, CuentaId = movimiento.CuentaId, Tipo = movimiento.Tipo, Monto = movimiento.Monto, Fecha = fecha });
        }
        db.MovimientosFinancieros.AddRange(rows); await db.SaveChangesAsync(ct);
        return rows.Select(r => r.Id).ToArray();
    }

    public async Task<bool> MarcarCompletadaAsync(int compraId, CancellationToken ct)
    {
        ExigirTransaccion();
        string publicId = (await db.Compras.Where(c => c.Id == compraId).Select(c => c.PublicId).SingleAsync(ct)).ToString("D");
        if (await db.OutboxMensajes.AnyAsync(o => o.Agregado == "Compra" && o.AgregadoId == publicId && o.Tipo == "compra.completada", ct)) return false;
        return await db.Compras.Where(c => c.Id == compraId && c.Estado == "PROCESANDO")
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, "COMPLETADA"), ct) == 1;
    }

    public async Task<bool> EmisionYOutboxCompletosAsync(CompraParaCobro compra, CancellationToken ct)
    {
        ExigirTransaccion();
        var detalles = await db.DetalleCompra.Where(d => d.CompraId == compra.Id).Select(d => new
        { d.Cantidad, Emitidos = db.Boletos.Count(b => b.DetalleCompraId == d.Id) }).ToArrayAsync(ct);
        return detalles.Length > 0 && detalles.All(d => d.Cantidad > 0 && d.Emitidos == d.Cantidad)
            && await db.OutboxMensajes.CountAsync(o => o.Agregado == "Compra" && o.AgregadoId == compra.PublicId.ToString("D") && o.Tipo == "compra.completada", ct) == 1;
    }

    private void ExigirTransaccion()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("El cobro requiere la transacción de finalización.");
    }
}
