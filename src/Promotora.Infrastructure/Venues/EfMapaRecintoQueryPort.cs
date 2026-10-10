using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Venues;
using Promotora.Application.Venues.Ports;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Venues;

public sealed class EfMapaRecintoQueryPort(PromotoraDbContext db) : IMapaRecintoQueryPort
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<MapaRecintoDto?> ReadAsync(int recintoId, int? eventoId, CancellationToken ct)
    {
        var recinto = await db.Recintos.AsNoTracking().Where(x => x.Id == recintoId && x.Activo)
            .Select(x => new { x.Id, x.Nombre, x.ImagenPlanoUrl }).SingleOrDefaultAsync(ct);
        if (recinto is null)
        {
            return null;
        }

        string? estado = null;
        if (eventoId.HasValue)
        {
            estado = await db.Eventos.AsNoTracking().Where(x => x.Id == eventoId.Value && x.RecintoId == recintoId
                && (x.Estado == "PROGRAMADO" || x.Estado == "EN_VENTA" || x.Estado == "AGOTADO"))
                .Select(x => x.Estado).SingleOrDefaultAsync(ct);
            if (estado is null)
            {
                return null;
            }
        }

        var localidades = await db.Localidades.AsNoTracking().Where(x => x.RecintoId == recintoId && x.Activo)
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.Nombre, x.Capacidad, x.Geometria, x.Color }).ToListAsync(ct);
        var ids = localidades.Select(x => x.Id).ToArray();
        var tipos = eventoId.HasValue ? await db.TiposEntrada.AsNoTracking()
            .Where(x => x.EventoId == eventoId.Value && x.Activo && (x.LocalidadId == null || ids.Contains(x.LocalidadId.Value)))
            .OrderBy(x => x.Id).Select(x => new { x.LocalidadId, Dto = new TipoEntradaMapaDto(x.Id, x.Nombre, x.Precio, x.CupoTotal, x.CupoDisponible) }).ToListAsync(ct) : [];
        var porLocalidad = tipos.Where(x => x.LocalidadId.HasValue).ToLookup(x => x.LocalidadId!.Value, x => x.Dto);
        return new MapaRecintoDto(recinto.Id, recinto.Nombre, recinto.ImagenPlanoUrl, eventoId, estado,
            localidades.Select(x => new LocalidadMapaDto(x.Id, x.Nombre, x.Capacidad,
                x.Geometria is null ? null : x.Geometria.Deserialize<PoligonoDto>(JsonOptions), x.Color,
                eventoId.HasValue ? porLocalidad[x.Id].Sum(y => (long)y.CupoDisponible) : null, porLocalidad[x.Id].ToArray())).ToArray(),
            tipos.Where(x => x.LocalidadId is null).Select(x => x.Dto).ToArray());
    }
}
