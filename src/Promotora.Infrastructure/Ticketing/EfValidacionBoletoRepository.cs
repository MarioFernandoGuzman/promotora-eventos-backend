using Microsoft.EntityFrameworkCore;
using Promotora.Application.Ticketing.Ports;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Ticketing;

public sealed class EfValidacionBoletoRepository(PromotoraDbContext db) : IValidacionBoletoRepository
{
    public async Task<BoletoParaValidar?> LockAsync(Guid publicId, string codigoQr, CancellationToken ct)
    {
        ExigirTransaccion();
        var query = from b in db.Boletos.AsNoTracking()
                    join d in db.DetalleCompra on b.DetalleCompraId equals d.Id
                    join c in db.Compras on d.CompraId equals c.Id
                    join e in db.Eventos on c.EventoId equals e.Id
                    join r in db.Recintos on e.RecintoId equals r.Id
                    where b.PublicId == publicId && b.CodigoQr == codigoQr
                    select new { b.Id, b.PublicId, r.DuenoId, b.Estado, EstadoCompra = c.Estado, RecintoId = r.Id, EventoId = e.Id, CompraId = c.Id };
        var inicial = await query.SingleOrDefaultAsync(ct);
        if (inicial is null) return null;
        // Orden compatible con catálogo/compra; bloquea transferencia y reembolso hasta confirmar el escaneo.
        await db.Recintos.FromSqlInterpolated($"SELECT * FROM recintos WHERE id={inicial.RecintoId} FOR SHARE").AsNoTracking().ToListAsync(ct);
        await db.Eventos.FromSqlInterpolated($"SELECT * FROM eventos WHERE id={inicial.EventoId} FOR SHARE").AsNoTracking().ToListAsync(ct);
        await db.Compras.FromSqlInterpolated($"SELECT * FROM compras WHERE id={inicial.CompraId} FOR SHARE").AsNoTracking().ToListAsync(ct);
        var actual = await query.SingleOrDefaultAsync(ct);
        if (actual is null || actual.RecintoId != inicial.RecintoId || actual.EventoId != inicial.EventoId || actual.CompraId != inicial.CompraId) return null;
        return new(actual.Id, actual.PublicId, actual.DuenoId, actual.Estado, actual.EstadoCompra);
    }

    public async Task<bool> UsarAsync(long id, string codigoQr, int usuarioId, DateTime fechaUso, CancellationToken ct)
    {
        ExigirTransaccion();
        if (fechaUso.Kind != DateTimeKind.Utc) throw new ArgumentException("La fecha debe ser UTC.", nameof(fechaUso));
        return await db.Boletos.Where(b => b.Id == id && b.CodigoQr == codigoQr && b.Estado == "VALIDO")
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Estado, "USADO").SetProperty(b => b.FechaUso, fechaUso).SetProperty(b => b.ValidadoPor, usuarioId), ct) == 1;
    }

    private void ExigirTransaccion()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("La validación de entrada requiere una transacción explícita.");
    }
}
