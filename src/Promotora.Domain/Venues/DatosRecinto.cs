namespace Promotora.Domain.Venues;

public sealed record DatosRecinto(string Nombre, string? Direccion, string? Ciudad, decimal? Latitud,
    decimal? Longitud, string? ImagenPlanoUrl, int? CapacidadTotal);
