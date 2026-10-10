using Promotora.Domain.Catalog;

namespace Promotora.Domain.Tests;

public sealed class CatalogoTests
{
    [Fact]
    public void CategoriaInactivaNoSeEditaYNombreNoPuedeEstarVacio()
    {
        Assert.Throws<ArgumentException>(() => new CategoriaEvento(1, new DatosCategoriaEvento(" ", null), true));
        var categoria = new CategoriaEvento(1, new DatosCategoriaEvento("Concierto", null), false);
        Assert.True(categoria.Editar(new DatosCategoriaEvento("Otro", null)).IsFailure);
        Assert.Equal("Concierto", categoria.Datos.Nombre);
    }

    [Fact]
    public void PresentadorSoloAdmiteTiposDocumentados()
    {
        Assert.Throws<ArgumentException>(() => new Presentador(1, new DatosPresentador("Artista", "DJ", null, null), true));
        var presentador = new Presentador(1, new DatosPresentador("Artista", "ARTISTA", null, null), true);
        Assert.True(presentador.Editar(new DatosPresentador("Equipo", "EQUIPO", null, null)).IsSuccess);
        Assert.Equal("EQUIPO", presentador.Datos.Tipo);
    }
}
