using Promotora.Domain.Venues;

namespace Promotora.WebApi.Venues;

public sealed record CrearRecintoRequest(int? DuenoId, string Nombre, string? Direccion = null, string? Ciudad = null,
    decimal? Latitud = null, decimal? Longitud = null, string? ImagenPlanoUrl = null, int? CapacidadTotal = null)
{
    public DatosRecinto ToDomain() => new(Nombre, Direccion, Ciudad, Latitud, Longitud, ImagenPlanoUrl, CapacidadTotal);
}

public sealed record ActualizarRecintoRequest(string Nombre, string? Direccion = null, string? Ciudad = null,
    decimal? Latitud = null, decimal? Longitud = null, string? ImagenPlanoUrl = null, int? CapacidadTotal = null)
{
    public DatosRecinto ToDomain() => new(Nombre, Direccion, Ciudad, Latitud, Longitud, ImagenPlanoUrl, CapacidadTotal);
}
