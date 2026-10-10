using Promotora.Domain.Venues;

namespace Promotora.Domain.Tests;

public sealed class RecintoTests
{
    [Fact]
    public void EdicionNoPermiteReducirCapacidadPorDebajoDeLocalidadesInactivasNiQuitarla()
    {
        var datos = new DatosRecinto("Recinto", null, null, null, null, null, 100);
        var recinto = new Recinto(1, 2, datos, true, DateTime.UtcNow);
        Assert.True(recinto.Editar(datos with { CapacidadTotal = 50 }, 60, true).IsFailure);
        Assert.True(recinto.Editar(datos with { CapacidadTotal = null }, 60, true).IsFailure);
        Assert.Equal(100, recinto.Datos.CapacidadTotal);
        Assert.True(recinto.Editar(datos with { CapacidadTotal = 60 }, 60, true).IsSuccess);
    }

    [Fact]
    public void RecintoSinLocalidadesAdmiteCapacidadSinConfigurarYBajaImpideEditar()
    {
        var datos = new DatosRecinto("Recinto", null, null, null, null, null, null);
        Assert.True(new Recinto(1, 2, datos, true, DateTime.UtcNow).Editar(datos, 0, false).IsSuccess);
        Assert.True(new Recinto(1, 2, datos, false, DateTime.UtcNow).Editar(datos, 0, false).IsFailure);
    }
}
