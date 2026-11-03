using Promotora.Domain.Finance;
using Promotora.Domain.Ticketing;

namespace Promotora.Domain.Tests;

public sealed class MovimientosReembolsoTests
{
    private static readonly ImportesCompra Importes = ImportesCompra.DesdeMontos(300m, 30m, 39.60m, 369.60m).Value;
    private static readonly MovimientoOriginal[] Originales = [new(1, 1, "VENTA", 300m), new(2, 2, "COMISION", 30m), new(3, 3, "IMPUESTO", 39.60m)];
    private static readonly CuentaCobro[] Cuentas = [new(1, "DUENO", 1, "GTQ", 300m, "ACTIVA"), new(2, "PLATAFORMA", null, "GTQ", 30m, "ACTIVA"), new(3, "IMPUESTOS", null, "GTQ", 39.60m, "ACTIVA")];

    [Fact]
    public void ReversaExactaDeLosImportesCongeladosSinPoliticaVigente()
    {
        var plan = MovimientosReembolso.Invertir(Importes, "GTQ", Originales, Cuentas);
        Assert.True(plan.IsSuccess); Assert.Equal(-369.60m, plan.Value.Sum(m => m.Monto));
        Assert.All(plan.Value, m => { Assert.Equal("REEMBOLSO", m.Tipo); Assert.Equal(-Originales.Single(o => o.CuentaId == m.CuentaId).Monto, m.Monto); });
        Assert.True(MovimientosReembolso.ValidarDisponibilidad(plan.Value, Cuentas).IsSuccess);
    }

    [Theory]
    [InlineData("duplicado")]
    [InlineData("faltante")]
    [InlineData("monto")]
    [InlineData("cuenta")]
    public void RespaldoInconsistenteNoGeneraReembolso(string caso)
    {
        var originales = Originales.ToArray(); var cuentas = Cuentas.ToArray();
        if (caso == "duplicado") originales = [.. originales, originales[0]];
        if (caso == "faltante") originales = originales[..2];
        if (caso == "monto") originales[2] = originales[2] with { Monto = 40m };
        if (caso == "cuenta") cuentas[0] = cuentas[0] with { Tipo = "IMPUESTOS" };
        Assert.True(MovimientosReembolso.Invertir(Importes, "GTQ", originales, cuentas).IsFailure);
    }

    [Theory]
    [InlineData("CERRADA", 300)]
    [InlineData("ACTIVA", 299)]
    public void NoPermiteDeudaNiMovimientosEnCuentaCerrada(string estado, int saldo)
    {
        var cuentas = Cuentas.ToArray(); cuentas[0] = cuentas[0] with { Estado = estado, Saldo = saldo };
        var plan = MovimientosReembolso.Invertir(Importes, "GTQ", Originales, cuentas).Value;
        Assert.True(MovimientosReembolso.ValidarDisponibilidad(plan, cuentas).IsFailure);
    }

    [Fact]
    public void GratisNoGeneraMovimientosYComponentesCeroSeOmiten()
    {
        Assert.Empty(MovimientosReembolso.Invertir(ImportesCompra.DesdeMontos(0, 0, 0, 0).Value, "GTQ", [], []).Value);
        var sinCargos = ImportesCompra.DesdeMontos(300, 0, 0, 300).Value;
        Assert.Single(MovimientosReembolso.Invertir(sinCargos, "GTQ", Originales[..1], Cuentas[..1]).Value);
    }
}
