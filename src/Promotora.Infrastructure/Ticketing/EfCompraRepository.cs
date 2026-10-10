using Microsoft.EntityFrameworkCore;
using Promotora.Application.Ticketing;
using Promotora.Application.Ticketing.Ports;
using Promotora.Domain.Ticketing;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Ticketing.Persistence;

namespace Promotora.Infrastructure.Ticketing;

public sealed class EfCompraRepository(PromotoraDbContext db) : ICompraRepository
{
    public Task<int?> ClienteIdAsync(int usuarioId, CancellationToken ct) => db.Clientes.Where(x => x.UsuarioId == usuarioId).Select(x => (int?)x.Id).SingleOrDefaultAsync(ct);

    public async Task<CompraExistente?> FindByKeyAsync(string key, CancellationToken ct)
    {
        var c = await db.Compras.AsNoTracking().SingleOrDefaultAsync(x => x.IdempotencyKey == key, ct);
        if (c is null) return null;
        var items = await db.DetalleCompra.AsNoTracking().Where(d => d.CompraId == c.Id).OrderBy(d => d.Id)
            .Select(d => new DetalleCompraDto(d.TipoEntradaId, d.Cantidad, d.PrecioUnitario, d.Subtotal)).ToArrayAsync(ct);
        return new(c.ClienteId, new(c.PublicId, c.EventoId, c.Estado, c.FechaCompra, c.ExpiraEn, c.Moneda, c.Subtotal, c.ComisionPlataforma, c.Impuestos, c.Total, items));
    }

    public async Task<EventoCompra?> LockEventoAsync(int eventoId, CancellationToken ct)
    {
        ExigirTransaccion();
        var initial = await db.Eventos.AsNoTracking().SingleOrDefaultAsync(e => e.Id == eventoId, ct);
        if (initial is null) return null;
        // SHARE permite compradores simultáneos y bloquea cambios administrativos.
        await db.Recintos.FromSqlInterpolated($"SELECT * FROM recintos WHERE id={initial.RecintoId} FOR SHARE").AsNoTracking().ToListAsync(ct);
        var eventos = await db.Eventos.FromSqlInterpolated($"SELECT * FROM eventos WHERE id={eventoId} FOR SHARE").AsNoTracking().ToListAsync(ct);
        var evento = eventos.SingleOrDefault();
        if (evento is null) return null;
        await db.CategoriasEvento.FromSqlInterpolated($"SELECT * FROM categorias_evento WHERE id={evento.CategoriaId} FOR SHARE").AsNoTracking().ToListAsync(ct);
        bool activo = evento.RecintoId == initial.RecintoId
            && await db.Recintos.AnyAsync(r => r.Id == evento.RecintoId && r.Activo, ct)
            && await db.CategoriasEvento.AnyAsync(c => c.Id == evento.CategoriaId && c.Activo, ct);
        return new(evento.Id, evento.Estado, activo, evento.FechaInicioVenta, evento.FechaFinVenta, evento.MaxBoletosPorCompra);
    }

    public async Task<IReadOnlyList<EntradaDisponible>> LockEntradasAsync(int eventoId, IReadOnlyList<int> tipos, CancellationToken ct)
    {
        ExigirTransaccion();
        int[] ids = tipos.Order().ToArray();
        int[] localidades = await db.TiposEntrada.Where(t => t.EventoId == eventoId && ids.Contains(t.Id) && t.LocalidadId != null)
            .Select(t => t.LocalidadId!.Value).Distinct().Order().ToArrayAsync(ct);
        await db.Localidades.FromSqlInterpolated($"SELECT * FROM localidades WHERE id = ANY({localidades}) ORDER BY id FOR SHARE").AsNoTracking().ToListAsync(ct);
        await db.TiposEntrada.FromSqlInterpolated($"SELECT * FROM tipos_entrada WHERE evento_id={eventoId} AND id = ANY({ids}) ORDER BY id FOR UPDATE").AsNoTracking().ToListAsync(ct);
        return await db.TiposEntrada.AsNoTracking().Where(t => t.EventoId == eventoId && ids.Contains(t.Id) && t.Activo
            && (t.LocalidadId == null || db.Localidades.Any(l => l.Id == t.LocalidadId && l.Activo)))
            .OrderBy(t => t.Id).Select(t => new EntradaDisponible(t.Id, t.EventoId, t.Precio)).ToArrayAsync(ct);
    }

    public async Task<int> InsertAsync(Compra compra, CancellationToken ct)
    {
        ExigirTransaccion();
        var c = new CompraRow
        {
            PublicId = compra.PublicId,
            ClienteId = compra.ClienteId,
            EventoId = compra.EventoId,
            Estado = compra.Estado.ToString(),
            FechaCompra = compra.FechaCompra,
            ExpiraEn = compra.ExpiraEn,
            IdempotencyKey = compra.IdempotencyKey,
            Moneda = compra.Moneda,
            Subtotal = compra.Importes.Subtotal,
            ComisionPlataforma = compra.Importes.ComisionPlataforma,
            Impuestos = compra.Importes.Impuestos,
            Total = compra.Importes.Total
        };
        db.Compras.Add(c);
        await db.SaveChangesAsync(ct);
        db.DetalleCompra.AddRange(compra.Detalles.Select(d => new DetalleCompraRow
        {
            CompraId = c.Id,
            TipoEntradaId = d.TipoEntradaId,
            Cantidad = d.Cantidad,
            PrecioUnitario = d.PrecioUnitario,
            Subtotal = d.Subtotal
        }));
        await db.SaveChangesAsync(ct);
        return c.Id;
    }

    private void ExigirTransaccion()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("La creación de compras requiere transacción explícita.");
    }
}
