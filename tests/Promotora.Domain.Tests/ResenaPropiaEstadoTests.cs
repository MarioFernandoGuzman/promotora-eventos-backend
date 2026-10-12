using Promotora.Domain.Reviews;

namespace Promotora.Domain.Tests;

public sealed class ResenaPropiaEstadoTests
{
    [Theory]
    [InlineData("APROBADA", "APROBADA", false, true)]
    [InlineData("APROBADA", "PENDIENTE", false, false)]
    [InlineData("RECHAZADA", "RECHAZADA", false, true)]
    [InlineData("RECHAZADA", "APROBADA", true, true)]
    [InlineData("APROBADA", "APROBADA", true, true)]
    [InlineData("APROBADA", "PENDIENTE", true, false)]
    public void EditarYRetirarValidanEstadoEsperado(string actual, string esperado, bool retirar, bool valido) =>
        Assert.Equal(valido, (retirar ? EstadoResena.Retirar(actual, esperado) : EstadoResena.Editar(actual, esperado, false)).IsSuccess);
}
