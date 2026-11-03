using Promotora.Domain.Reviews;

namespace Promotora.Domain.Tests;

public sealed class CalificacionMediaTests
{
    [Fact]
    public void SinCalificacionesNoEsUnaNotaCero() => Assert.Null(CalificacionMedia.Calcular(0, 0));
    [Theory]
    [InlineData(13, 3, 4.33)]
    [InlineData(25, 8, 3.13)]
    [InlineData(23, 5, 4.60)]
    public void MediaPorResenaConDosDecimales(long suma, long cantidad, decimal esperado) => Assert.Equal(esperado, CalificacionMedia.Calcular(suma, cantidad));
}
