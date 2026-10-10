using Promotora.Domain.Venues;

namespace Promotora.Application.Venues;

public sealed record RecintoDto(int Id, int DuenoId, string Nombre, string? Direccion, string? Ciudad,
    decimal? Latitud, decimal? Longitud, string? ImagenPlanoUrl, int? CapacidadTotal, bool Activo, DateTime FechaCreacion)
{
    public static RecintoDto From(Recinto recinto) => new(recinto.Id, recinto.DuenoId, recinto.Datos.Nombre,
        recinto.Datos.Direccion, recinto.Datos.Ciudad, recinto.Datos.Latitud, recinto.Datos.Longitud,
        recinto.Datos.ImagenPlanoUrl, recinto.Datos.CapacidadTotal, recinto.Activo, recinto.FechaCreacion);
}

public sealed record RecintosPaginaDto(IReadOnlyList<RecintoDto> Items, int Page, int PageSize, int Total);
