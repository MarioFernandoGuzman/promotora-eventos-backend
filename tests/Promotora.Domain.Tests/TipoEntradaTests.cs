using Promotora.Domain.Catalog;

namespace Promotora.Domain.Tests;

public sealed class TipoEntradaTests
{
    [Fact]
    public void CuposAcumuladosUsanLongYNoExcedenLocalidad()
    {
        Assert.True(TipoEntrada.ValidarCapacidad(60, 40, 100).IsSuccess);
        Assert.True(TipoEntrada.ValidarCapacidad(60, 41, 100).IsFailure);
        Assert.True(TipoEntrada.ValidarCapacidad(int.MaxValue, int.MaxValue, int.MaxValue).IsFailure);
    }
    [Fact]
    public void EdicionRechazaPrecioNoRepresentableEInventarioReservado()
    {
        Assert.Throws<ArgumentException>(() => new TipoEntrada(1, 1, null, new DatosTipoEntrada("General", 1.001m, 10), 10, true));
        var tipo = new TipoEntrada(1, 1, null, new DatosTipoEntrada("General", 10, 10), 9, true);
        Assert.True(tipo.Editar(new DatosTipoEntrada("General", 10, 20)).IsFailure);
        Assert.Equal(9, tipo.CupoDisponible);
        Assert.Equal(10, tipo.Datos.CupoTotal);
    }
}
