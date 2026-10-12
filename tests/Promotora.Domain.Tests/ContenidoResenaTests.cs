using Promotora.Domain.Reviews;

namespace Promotora.Domain.Tests;

public sealed class ContenidoResenaTests
{
    [Theory]
    [InlineData(1, 510, true)]
    [InlineData(5, 510, true)]
    [InlineData(5, 511, false)]
    [InlineData(0, 20, false)]
    [InlineData(6, 20, false)]
    public void LimitesDeCalificacionYComentario(short nota, int longitud, bool valido) =>
        Assert.Equal(valido, ContenidoResena.Crear(nota, new string('x', longitud)).IsSuccess);
    [Fact]
    public void ComentarioOpcionalYNormalizado()
    {
        Assert.Null(ContenidoResena.Crear(5, null).Value.Comentario);
        Assert.Null(ContenidoResena.Crear(5, "  ").Value.Comentario);
        Assert.Equal("Evento", ContenidoResena.Crear(5, " Evento ").Value.Comentario);
    }
}
