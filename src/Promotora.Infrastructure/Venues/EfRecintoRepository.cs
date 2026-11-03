using Microsoft.EntityFrameworkCore;
using Promotora.Application.Venues;
using Promotora.Application.Venues.Ports;
using Promotora.Domain.Venues;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Venues.Persistence;

namespace Promotora.Infrastructure.Venues;

public sealed class EfRecintoRepository(PromotoraDbContext db) : IRecintoRepository
{
    public Task<int?> DuenoIdAsync(int usuarioId, CancellationToken ct) => db.DuenosRecinto
        .Where(x => x.UsuarioId == usuarioId).Select(x => (int?)x.Id).SingleOrDefaultAsync(ct);

    public Task<bool> DuenoActivoAsync(int duenoId, CancellationToken ct) =>
        (from owner in db.DuenosRecinto
         join user in db.Usuarios on owner.UsuarioId equals user.Id
         where owner.Id == duenoId && user.Estado == "ACTIVO" && user.Rol == "DUENO_RECINTO"
         select owner.Id).AnyAsync(ct);

    public async Task<Recinto?> FindAsync(int id, bool bloquear, CancellationToken ct)
    {
        if (bloquear)
        {
            // FOR UPDATE requiere transacción; también serializa cambios en capacidades y localidades.
            await db.Recintos.FromSqlInterpolated($"SELECT * FROM recintos WHERE id={id} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        }

        var row = await db.Recintos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : ToDomain(row);
    }

    public async Task<Recinto> InsertAsync(Recinto recinto, CancellationToken ct)
    {
        var datos = recinto.Datos;
        var row = new RecintoRow
        {
            DuenoId = recinto.DuenoId,
            Nombre = datos.Nombre,
            Direccion = datos.Direccion,
            Ciudad = datos.Ciudad,
            Latitud = datos.Latitud,
            Longitud = datos.Longitud,
            ImagenPlanoUrl = datos.ImagenPlanoUrl,
            CapacidadTotal = datos.CapacidadTotal,
            Activo = true,
            FechaCreacion = recinto.FechaCreacion
        };
        db.Recintos.Add(row);
        await db.SaveChangesAsync(ct);
        return ToDomain(row);
    }

    public async Task UpdateAsync(Recinto recinto, CancellationToken ct)
    {
        var datos = recinto.Datos;
        await db.Recintos.Where(x => x.Id == recinto.Id && x.Activo).ExecuteUpdateAsync(setter => setter
            .SetProperty(x => x.Nombre, datos.Nombre).SetProperty(x => x.Direccion, datos.Direccion)
            .SetProperty(x => x.Ciudad, datos.Ciudad).SetProperty(x => x.Latitud, datos.Latitud)
            .SetProperty(x => x.Longitud, datos.Longitud).SetProperty(x => x.ImagenPlanoUrl, datos.ImagenPlanoUrl)
            .SetProperty(x => x.CapacidadTotal, datos.CapacidadTotal), ct);
    }

    public async Task<bool> DeactivateAsync(int id, CancellationToken ct) => await db.Recintos.Where(x => x.Id == id && x.Activo)
        .ExecuteUpdateAsync(setter => setter.SetProperty(x => x.Activo, false), ct) == 1;

    public async Task<(long Total, bool TieneLocalidades)> CapacidadAsync(int recintoId, CancellationToken ct)
    {
        var query = db.Localidades.Where(x => x.RecintoId == recintoId);
        return (await query.SumAsync(x => (long)x.Capacidad, ct), await query.AnyAsync(ct));
    }

    public async Task<RecintosPaginaDto> ListAsync(int? duenoId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Recintos.AsNoTracking().Where(x => !duenoId.HasValue || x.DuenoId == duenoId.Value);
        int total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new RecintosPaginaDto(rows.Select(x => RecintoDto.From(ToDomain(x))).ToArray(), page, pageSize, total);
    }

    private static Recinto ToDomain(RecintoRow row) => new(row.Id, row.DuenoId,
        new DatosRecinto(row.Nombre, row.Direccion, row.Ciudad, row.Latitud, row.Longitud, row.ImagenPlanoUrl, row.CapacidadTotal),
        row.Activo, row.FechaCreacion);
}
