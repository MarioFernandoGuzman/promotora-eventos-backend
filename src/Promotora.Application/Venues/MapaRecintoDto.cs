namespace Promotora.Application.Venues;

public sealed record TipoEntradaMapaDto(int Id, string Nombre, decimal Precio, int CupoTotal, int CupoDisponible);
public sealed record LocalidadMapaDto(int Id, string Nombre, int Capacidad, PoligonoDto? Geometria, string? Color,
    long? CupoDisponible, IReadOnlyList<TipoEntradaMapaDto> TiposEntrada);
public sealed record MapaRecintoDto(int Id, string Nombre, string? ImagenPlanoUrl, int? EventoId, string? EstadoEvento,
    IReadOnlyList<LocalidadMapaDto> Localidades, IReadOnlyList<TipoEntradaMapaDto> EntradasGenerales);
