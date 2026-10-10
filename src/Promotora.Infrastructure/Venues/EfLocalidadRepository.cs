using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Venues;
using Promotora.Application.Venues.Ports;
using Promotora.Domain.Venues;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Venues.Persistence;

namespace Promotora.Infrastructure.Venues;

public sealed class EfLocalidadRepository(PromotoraDbContext db) : ILocalidadRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Localidad?> FindAsync(int recintoId, int id, bool bloquear, CancellationToken ct)
    {
        if (bloquear)
        {
            await db.Localidades.FromSqlInterpolated($"SELECT * FROM localidades WHERE id={id} AND recinto_id={recintoId} FOR UPDATE")
                .AsNoTracking().ToListAsync(ct);
        }

        var row = await db.Localidades.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.RecintoId == recintoId, ct);
        return row is null ? null : ToDomain(row);
    }

    public async Task<Localidad> InsertAsync(Localidad localidad, CancellationToken ct)
    {
        var row = new LocalidadRow
        {
            RecintoId = localidad.RecintoId,
            Nombre = localidad.Nombre,
            Capacidad = localidad.Capacidad,
            Geometria = Geometry(localidad.Geometria),
            Color = localidad.Color,
            Activo = true
        };
        db.Localidades.Add(row);
        await db.SaveChangesAsync(ct);
        return ToDomain(row);
    }

    public async Task UpdateAsync(Localidad localidad, CancellationToken ct)
    {
        using var geometria = Geometry(localidad.Geometria);
        await db.Localidades.Where(x => x.Id == localidad.Id && x.RecintoId == localidad.RecintoId && x.Activo)
            .ExecuteUpdateAsync(setter => setter.SetProperty(x => x.Nombre, localidad.Nombre)
                .SetProperty(x => x.Capacidad, localidad.Capacidad).SetProperty(x => x.Geometria, geometria)
                .SetProperty(x => x.Color, localidad.Color), ct);
    }

    public async Task<bool> DeactivateAsync(int recintoId, int id, CancellationToken ct) => await db.Localidades
        .Where(x => x.Id == id && x.RecintoId == recintoId && x.Activo)
        .ExecuteUpdateAsync(setter => setter.SetProperty(x => x.Activo, false), ct) == 1;

    public Task<long> CapacidadOtrasAsync(int recintoId, int? excluirId, CancellationToken ct) => db.Localidades
        .Where(x => x.RecintoId == recintoId && (!excluirId.HasValue || x.Id != excluirId.Value)).SumAsync(x => (long)x.Capacidad, ct);

    public async Task<long> MaxCupoEventoAsync(int localidadId, CancellationToken ct) => await db.TiposEntrada
        .Where(x => x.LocalidadId == localidadId).GroupBy(x => x.EventoId).Select(x => (long?)x.Sum(y => (long)y.CupoTotal)).MaxAsync(ct) ?? 0;

    public async Task<LocalidadesPaginaDto> ListAsync(int recintoId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Localidades.AsNoTracking().Where(x => x.RecintoId == recintoId);
        int total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new LocalidadesPaginaDto(rows.Select(x => LocalidadDto.From(ToDomain(x))).ToArray(), page, pageSize, total);
    }

    private static JsonDocument? Geometry(Poligono? poligono) => poligono is null ? null
        : JsonSerializer.SerializeToDocument(PoligonoDto.From(poligono), JsonOptions);

    private static Localidad ToDomain(LocalidadRow row) => new(row.Id, row.RecintoId, row.Nombre, row.Capacidad,
        row.Geometria is null ? null : row.Geometria.Deserialize<PoligonoDto>(JsonOptions)!.ToDomain(), row.Color, row.Activo);
}
