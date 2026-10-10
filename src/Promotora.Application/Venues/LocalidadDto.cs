using Promotora.Domain.Venues;

namespace Promotora.Application.Venues;

public sealed record PoligonoDto(string Type, IReadOnlyList<IReadOnlyList<double>> Points)
{
    public bool EsValido() => Type == "polygon" && Points is not null && Points.Count >= 3
        && Points.All(x => x is not null && x.Count == 2 && double.IsFinite(x[0]) && double.IsFinite(x[1]))
        && Points.Select(x => (x[0], x[1])).Distinct().Count() >= 3;

    public Poligono ToDomain() => new(Points.Select(x => new Punto(x[0], x[1])));
    public static PoligonoDto? From(Poligono? geometria) => geometria is null ? null
        : new("polygon", geometria.Puntos.Select(x => (IReadOnlyList<double>)new[] { x.X, x.Y }).ToArray());
}

public sealed record DatosLocalidadDto(string Nombre, int Capacidad, PoligonoDto? Geometria = null, string? Color = null);
public sealed record LocalidadDto(int Id, int RecintoId, string Nombre, int Capacidad, PoligonoDto? Geometria, string? Color, bool Activo)
{
    public static LocalidadDto From(Localidad localidad) => new(localidad.Id, localidad.RecintoId, localidad.Nombre,
        localidad.Capacidad, PoligonoDto.From(localidad.Geometria), localidad.Color, localidad.Activo);
}

public sealed record LocalidadesPaginaDto(IReadOnlyList<LocalidadDto> Items, int Page, int PageSize, int Total);
