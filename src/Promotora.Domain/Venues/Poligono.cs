namespace Promotora.Domain.Venues;

public sealed record Punto(double X, double Y);

public sealed class Poligono
{
    public IReadOnlyList<Punto> Puntos { get; }

    public Poligono(IEnumerable<Punto> puntos)
    {
        var copy = puntos.ToArray();
        if (copy.Length < 3 || copy.Any(x => !double.IsFinite(x.X) || !double.IsFinite(x.Y)) || copy.Distinct().Count() < 3)
        {
            throw new ArgumentException("El polígono requiere al menos tres puntos distintos con coordenadas finitas.");
        }

        Puntos = Array.AsReadOnly(copy);
    }
}
