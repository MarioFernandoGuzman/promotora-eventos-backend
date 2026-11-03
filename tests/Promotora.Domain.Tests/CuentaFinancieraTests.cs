using Promotora.Domain.Finance;

namespace Promotora.Domain.Tests;

public sealed class CuentaFinancieraTests
{
    [Fact]
    public void CierreConSaldoCeroEsIdempotenteYConservaFecha()
    {
        var ahora = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var cuenta = new CuentaFinanciera(1, "DUENO", 2, "GTQ", 0m, "ACTIVA", null);
        Assert.True(cuenta.Cerrar(ahora).IsSuccess);
        Assert.True(cuenta.Cerrar(ahora.AddMinutes(1)).IsSuccess);
        Assert.Equal("CERRADA", cuenta.Estado); Assert.Equal(ahora, cuenta.FechaCierre);
    }

    [Theory]
    [InlineData("PLATAFORMA")]
    [InlineData("IMPUESTOS")]
    public void CuentasDelSistemaNoSeCierran(string tipo)
    {
        var cuenta = new CuentaFinanciera(1, tipo, null, "GTQ", 0m, "ACTIVA", null);
        Assert.Equal("cuenta.tipo", Assert.Single(cuenta.Cerrar(DateTime.UtcNow).Errors).Code);
        Assert.Equal("ACTIVA", cuenta.Estado);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void SaldoNoCeroImpideCierre(int saldo)
    {
        var cuenta = new CuentaFinanciera(1, "DUENO", 2, "GTQ", saldo, "ACTIVA", null);
        Assert.Equal("cuenta.saldo", Assert.Single(cuenta.Cerrar(DateTime.UtcNow).Errors).Code);
        Assert.Null(cuenta.FechaCierre);
    }

    [Fact]
    public void CierreRechazaFechaSinUtc()
    {
        var cuenta = new CuentaFinanciera(1, "DUENO", 2, "GTQ", 0m, "ACTIVA", null);
        Assert.True(cuenta.Cerrar(new DateTime(2030, 1, 1)).IsFailure); Assert.Equal("ACTIVA", cuenta.Estado);
    }
}
