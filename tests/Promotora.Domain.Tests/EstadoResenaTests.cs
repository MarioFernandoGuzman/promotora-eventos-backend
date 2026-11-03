using Promotora.Domain.Reviews;

namespace Promotora.Domain.Tests;

public sealed class EstadoResenaTests
{
    [Theory]
    [InlineData("PENDIENTE", "APROBADA", true)]
    [InlineData("PENDIENTE", "RECHAZADA", true)]
    [InlineData("APROBADA", "APROBADA", true)]
    [InlineData("APROBADA", "RECHAZADA", false)]
    [InlineData("RECHAZADA", "APROBADA", false)]
    [InlineData("PENDIENTE", "PENDIENTE", false)]
    public void ModeracionRespetaTransicionesYRepeticion(string actual, string nuevo, bool valido) => Assert.Equal(valido, EstadoResena.Moderar(actual, nuevo).IsSuccess);
}
