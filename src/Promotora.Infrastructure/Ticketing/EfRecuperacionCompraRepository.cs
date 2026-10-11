using Microsoft.EntityFrameworkCore;
using Promotora.Application.Ticketing.Ports;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Ticketing;

public sealed class EfRecuperacionCompraRepository(PromotoraDbContext db) : IRecuperacionCompraRepository
{
    public async Task<IReadOnlyList<MensajeCompraDto>> CandidatasAsync(DateTime limite, int lote, CancellationToken ct)
        => await db.Database.SqlQuery<MensajeCompraDto>($"""
            SELECT o.id AS mensaje_id, c.public_id
            FROM public.compras c JOIN public.outbox_mensajes o
              ON o.agregado_id=c.public_id::text AND o.agregado='Compra' AND o.tipo='compra.procesar'
            WHERE c.estado='PROCESANDO' AND c.procesando_desde <= {limite}
            ORDER BY c.procesando_desde,c.id LIMIT {lote}
            """).ToArrayAsync(ct);

    public async Task<CompraTomadaDto?> LockAsync(Guid publicId, DateTime limite, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("La recuperación requiere transacción explícita.");
        var rows = await db.Compras.FromSqlInterpolated($"SELECT * FROM public.compras WHERE public_id={publicId} AND estado='PROCESANDO' AND procesando_desde <= {limite} FOR UPDATE SKIP LOCKED").AsNoTracking().ToArrayAsync(ct);
        var c = rows.SingleOrDefault();
        return c is null ? null : new(c.Id, c.PublicId, c.Subtotal, c.ComisionPlataforma, c.Impuestos, c.Total, c.Moneda);
    }

    public async Task<bool> TieneEfectosConfirmadosAsync(int compraId, CancellationToken ct)
        => await db.Pagos.AnyAsync(p => p.CompraId == compraId && (p.Estado == "APROBADO" || p.Estado == "REEMBOLSADO"), ct)
            || await db.MovimientosFinancieros.AnyAsync(m => m.CompraId == compraId, ct)
            || await db.Boletos.AnyAsync(b => db.DetalleCompra.Any(d => d.Id == b.DetalleCompraId && d.CompraId == compraId), ct);
}
