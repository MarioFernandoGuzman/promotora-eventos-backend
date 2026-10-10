using Promotora.Domain.Venues;

namespace Promotora.Domain.Tests;

public sealed class LocalidadTests
{
    [Theory]
    [InlineData(70, 100, 40, 0, false)]
    [InlineData(50, 100, 40, 60, false)]
    [InlineData(60, 100, 40, 60, true)]
    public void CapacidadRespetaOtrasLocalidadesYCupoDeCadaEvento(int capacidad, int total, long otras, long cupo, bool permitido) =>
        Assert.Equal(permitido, Localidad.ValidarCapacidad(capacidad, total, otras, cupo).IsSuccess);

    [Fact]
    public void GeometriaYColorValidanFormaYProtegenPuntosDeMutaciones()
    {
        Assert.False(Localidad.ColorValido("rojo"));
        Assert.True(Localidad.ColorValido("#aAbB09"));
        Assert.True(Localidad.ColorValido(null));
        Assert.Throws<ArgumentException>(() => new Poligono([new(0, 0), new(1, 1), new(double.NaN, 2)]));
        Assert.Throws<ArgumentException>(() => new Poligono([new(0, 0), new(1, 1), new(0, 0)]));
        var puntos = new[] { new Punto(0, 0), new Punto(1, 0), new Punto(1, 1) };
        var polygon = new Poligono(puntos);
        puntos[0] = new Punto(9, 9);
        Assert.Equal(0, polygon.Puntos[0].X);
    }
}
