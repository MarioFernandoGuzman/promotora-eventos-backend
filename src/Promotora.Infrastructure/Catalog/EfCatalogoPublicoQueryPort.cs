using Microsoft.EntityFrameworkCore;
using Promotora.Application.Catalog;
using Promotora.Application.Catalog.Ports;
using Promotora.Application.Shared;
using Promotora.Infrastructure.Catalog.Persistence;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Catalog;

public sealed class EfCatalogoPublicoQueryPort(PromotoraDbContext db) : ICatalogoPublicoQueryPort
{
    private IQueryable<EventoRow> Publicos() => db.Eventos.AsNoTracking().Where(e =>
        (e.Estado == "PROGRAMADO" || e.Estado == "EN_VENTA" || e.Estado == "AGOTADO")
        && db.Recintos.Any(r => r.Id == e.RecintoId && r.Activo)
        && db.CategoriasEvento.Any(c => c.Id == e.CategoriaId && c.Activo));

    public async Task<PaginaDto<EventoResumenPublicoDto>> ListAsync(ListarCatalogoPublicoQuery request, CancellationToken ct)
    {
        var query = Publicos();
        if (request.CategoriaId is int categoria) query = query.Where(e => e.CategoriaId == categoria);
        if (!string.IsNullOrWhiteSpace(request.Ciudad))
        {
            string ciudad = request.Ciudad.Trim().ToLowerInvariant();
            query = query.Where(e => db.Recintos.Any(r => r.Id == e.RecintoId && r.Ciudad != null && r.Ciudad.ToLower() == ciudad));
        }
        if (request.Desde is DateTime desde) query = query.Where(e => e.FechaInicio >= desde);
        if (request.Hasta is DateTime hasta) query = query.Where(e => e.FechaInicio <= hasta);
        if (!string.IsNullOrWhiteSpace(request.Texto))
        {
            string texto = request.Texto.Trim().ToLowerInvariant();
            query = query.Where(e => e.Nombre.ToLower().Contains(texto) || (e.Descripcion != null && e.Descripcion.ToLower().Contains(texto)));
        }
        int total = await query.CountAsync(ct);
        var page = query.OrderBy(e => e.FechaInicio).ThenBy(e => e.Id).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize);
        var items = await (from e in page
                           join c in db.CategoriasEvento.AsNoTracking() on e.CategoriaId equals c.Id
                           join r in db.Recintos.AsNoTracking() on e.RecintoId equals r.Id
                           orderby e.FechaInicio, e.Id
                           select new EventoResumenPublicoDto(e.Id, e.Nombre, e.FechaInicio, e.FechaFin, e.Estado, e.ImagenUrl,
                               c.Id, c.Nombre, r.Id, r.Nombre, r.Ciudad)).ToListAsync(ct);
        return new(items, request.Page, request.PageSize, total);
    }

    public async Task<EventoPublicoDto?> FindAsync(int id, CancellationToken ct)
    {
        // Una sola instantánea evita mezclar un evento recién ocultado con sus hijos públicos.
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var evento = await (from e in Publicos()
                            where e.Id == id
                            join c in db.CategoriasEvento.AsNoTracking() on e.CategoriaId equals c.Id
                            join r in db.Recintos.AsNoTracking() on e.RecintoId equals r.Id
                            select new
                            {
                                Evento = e,
                                Categoria = new CategoriaPublicaDto(c.Id, c.Nombre),
                                Recinto = new RecintoPublicoDto(r.Id, r.Nombre, r.Ciudad, r.Direccion, r.Latitud, r.Longitud, r.ImagenPlanoUrl)
                            }).SingleOrDefaultAsync(ct);
        if (evento is null) return null;
        var presenters = await (from relation in db.EventoPresentadores.AsNoTracking()
                                join p in db.Presentadores.AsNoTracking() on relation.PresentadorId equals p.Id
                                where relation.EventoId == id && p.Activo
                                orderby p.Id
                                select new PresentadorPublicoDto(p.Id, p.Nombre, p.Tipo, relation.Rol, p.Biografia, p.ImagenUrl)).ToListAsync(ct);
        var tipos = await db.TiposEntrada.AsNoTracking().Where(t => t.EventoId == id && t.Activo
            && (t.LocalidadId == null || db.Localidades.Any(l => l.Id == t.LocalidadId && l.Activo))).OrderBy(t => t.Id)
            .Select(t => new TipoEntradaPublicoDto(t.Id, t.LocalidadId, t.Nombre, t.Precio, t.CupoTotal, t.CupoDisponible)).ToListAsync(ct);
        var row = evento.Evento;
        await tx.CommitAsync(ct);
        return new(row.Id, row.Nombre, row.Descripcion, row.FechaInicio, row.FechaFin, row.FechaInicioVenta, row.FechaFinVenta,
            row.MaxBoletosPorCompra, row.Estado, row.ImagenUrl, evento.Categoria, evento.Recinto, presenters, tipos);
    }
}
