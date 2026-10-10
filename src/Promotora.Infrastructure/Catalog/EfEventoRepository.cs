using Microsoft.EntityFrameworkCore;
using Promotora.Application.Catalog;
using Promotora.Application.Catalog.Ports;
using Promotora.Application.Shared;
using Promotora.Domain.Catalog;
using Promotora.Infrastructure.Catalog.Persistence;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Catalog;

public sealed class EfEventoRepository(PromotoraDbContext db) : IEventoRepository
{
    public async Task<Evento?> FindAsync(int id, bool bloquear, CancellationToken ct)
    {
        if (bloquear) await db.Eventos.FromSqlInterpolated($"SELECT * FROM eventos WHERE id={id} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        var row = await db.Eventos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : ToDomain(row);
    }

    public async Task<Evento> InsertAsync(Evento entity, CancellationToken ct)
    {
        var d = entity.Datos;
        var row = new EventoRow
        {
            RecintoId = d.RecintoId,
            CategoriaId = d.CategoriaId,
            CreadoPor = entity.CreadoPor,
            Nombre = d.Nombre,
            Descripcion = d.Descripcion,
            FechaInicio = d.FechaInicio,
            FechaFin = d.FechaFin,
            FechaInicioVenta = d.FechaInicioVenta,
            FechaFinVenta = d.FechaFinVenta,
            MaxBoletosPorCompra = d.MaxBoletosPorCompra,
            ImagenUrl = d.ImagenUrl,
            Estado = entity.Estado.ToString(),
            FechaCreacion = entity.FechaCreacion
        };
        db.Eventos.Add(row);
        await db.SaveChangesAsync(ct);
        return ToDomain(row);
    }

    public async Task UpdateAsync(Evento entity, CancellationToken ct)
    {
        var d = entity.Datos;
        await db.Eventos.Where(x => x.Id == entity.Id && x.Estado == entity.Estado.ToString()).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.CategoriaId, d.CategoriaId).SetProperty(x => x.Nombre, d.Nombre).SetProperty(x => x.Descripcion, d.Descripcion)
            .SetProperty(x => x.FechaInicio, d.FechaInicio).SetProperty(x => x.FechaFin, d.FechaFin)
            .SetProperty(x => x.FechaInicioVenta, d.FechaInicioVenta).SetProperty(x => x.FechaFinVenta, d.FechaFinVenta)
            .SetProperty(x => x.MaxBoletosPorCompra, d.MaxBoletosPorCompra).SetProperty(x => x.ImagenUrl, d.ImagenUrl), ct);
    }

    public async Task<bool> ChangeStateAsync(int id, EstadoEvento expected, EstadoEvento next, CancellationToken ct) =>
        await db.Eventos.Where(x => x.Id == id && x.Estado == expected.ToString()).ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, next.ToString()), ct) == 1;

    public Task<bool> TieneComprasAsync(int id, CancellationToken ct) => db.Compras.AnyAsync(x => x.EventoId == id, ct);

    public Task<long> CupoDisponibleAsync(int id, CancellationToken ct) => db.TiposEntrada.Where(x => x.EventoId == id && x.Activo
        && (x.LocalidadId == null || db.Localidades.Any(l => l.Id == x.LocalidadId && l.Activo))).SumAsync(x => (long)x.CupoDisponible, ct);

    public async Task<PaginaDto<EventoDto>> ListAsync(int? duenoId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Eventos.AsNoTracking().Where(x => duenoId == null || db.Recintos.Any(r => r.Id == x.RecintoId && r.DuenoId == duenoId));
        int total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows.Select(x => EventoDto.From(ToDomain(x))).ToArray(), page, pageSize, total);
    }

    internal static Evento ToDomain(EventoRow r) => new(r.Id, r.CreadoPor,
        new DatosEvento(r.RecintoId, r.CategoriaId, r.Nombre, r.Descripcion, r.FechaInicio, r.FechaFin, r.FechaInicioVenta,
            r.FechaFinVenta, r.MaxBoletosPorCompra, r.ImagenUrl), Enum.Parse<EstadoEvento>(r.Estado), r.FechaCreacion);
}
