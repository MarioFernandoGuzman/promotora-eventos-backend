using Promotora.Domain.Finance;
using Promotora.Domain.Shared;

namespace Promotora.Domain.Tests;

public sealed class LiquidacionTests
{
    [Theory]
    [InlineData(100, 40, -40)]
    [InlineData(100, 100, -100)]
    [InlineData(0.01, 0.01, -0.01)]
    public void LiquidacionEsUnDebitoSinMutarElSaldo(decimal saldo, decimal monto, decimal esperado)
    {
        var cuenta = new CuentaFinanciera(1, "DUENO", 1, "GTQ", saldo, "ACTIVA", null);
        Assert.Equal(esperado, cuenta.Liquidar(monto).Value); Assert.Equal(saldo, cuenta.Saldo);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.001)]
    [InlineData(10000000000)]
    public void MontoFueraDelContratoSeRechaza(decimal monto) => Assert.Equal(ErrorType.Validation,
        Assert.Single(new CuentaFinanciera(1, "DUENO", 1, "GTQ", 100m, "ACTIVA", null).Liquidar(monto).Errors).Type);
    [Theory]
    [InlineData("DUENO", "ACTIVA", 50)]
    [InlineData("DUENO", "CERRADA", 100)]
    [InlineData("PLATAFORMA", "ACTIVA", 100)]
    [InlineData("IMPUESTOS", "ACTIVA", 100)]
    public void CuentaNoAptaSeRechaza(string tipo, string estado, decimal saldo) => Assert.Equal(ErrorType.Conflict,
        Assert.Single(new CuentaFinanciera(1, tipo, tipo == "DUENO" ? 1 : null, "GTQ", saldo, estado, null).Liquidar(100m).Errors).Type);
}
