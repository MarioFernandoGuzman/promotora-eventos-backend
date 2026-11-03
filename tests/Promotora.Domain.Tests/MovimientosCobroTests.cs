using Promotora.Domain.Finance;
using Promotora.Domain.Ticketing;

namespace Promotora.Domain.Tests;

public sealed class MovimientosCobroTests
{
    [Theory]
    [InlineData("300.00", "30.00", "39.60", "369.60")]
    [InlineData("33.33", "3.33", "4.40", "41.06")]
    public void DistribuyeComponentesCongeladosSinRecalcularTasas(string subtotal, string comision, string iva, string total)
    {
        decimal Numero(string s) => decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        var importes = ImportesCompra.DesdeMontos(Numero(subtotal), Numero(comision), Numero(iva), Numero(total)).Value;
        var movimientos = MovimientosCobro.Crear(importes, 7, "GTQ", Cuentas()).Value;
        Assert.Equal(new MovimientoCobro(1, "VENTA", importes.Subtotal), movimientos[0]);
        Assert.Equal(new MovimientoCobro(2, "COMISION", importes.ComisionPlataforma), movimientos[1]);
        Assert.Equal(new MovimientoCobro(3, "IMPUESTO", importes.Impuestos), movimientos[2]);
        Assert.Equal(importes.Total, movimientos.Sum(m => m.Monto));
    }

    [Fact]
    public void GratuidadNoExigeCuentasNiGeneraMovimientosYComponentesCeroSeOmiten()
    {
        Assert.Empty(MovimientosCobro.Crear(ImportesCompra.DesdeMontos(0, 0, 0, 0).Value, 7, "GTQ", []).Value);
        var movimientos = MovimientosCobro.Crear(ImportesCompra.DesdeMontos(100, 0, 0, 100).Value, 7, "GTQ", [Cuentas()[0]]).Value;
        Assert.Equal(new MovimientoCobro(1, "VENTA", 100), Assert.Single(movimientos));
    }

    [Theory]
    [InlineData("cerrada")]
    [InlineData("moneda")]
    [InlineData("dueno")]
    [InlineData("faltante")]
    [InlineData("duplicada")]
    [InlineData("saldo")]
    public void NoGeneraCobrosConCuentasInvalidas(string caso)
    {
        var cuentas = Cuentas().ToList();
        if (caso == "cerrada") cuentas[0] = cuentas[0] with { Estado = "CERRADA" };
        if (caso == "moneda") cuentas[0] = cuentas[0] with { Moneda = "USD" };
        if (caso == "dueno") cuentas[0] = cuentas[0] with { DuenoId = 8 };
        if (caso == "faltante") cuentas.RemoveAt(0);
        if (caso == "duplicada") cuentas.Add(cuentas[0] with { Id = 4 });
        if (caso == "saldo") cuentas[0] = cuentas[0] with { Saldo = 9999999999.99m };
        Assert.True(MovimientosCobro.Crear(ImportesCompra.Calcular(300, new(0.12m, 0.10m)).Value, 7, "GTQ", cuentas).IsFailure);
    }

    private static CuentaCobro[] Cuentas() => [new(1, "DUENO", 7, "GTQ", 0, "ACTIVA"), new(2, "PLATAFORMA", null, "GTQ", 0, "ACTIVA"), new(3, "IMPUESTOS", null, "GTQ", 0, "ACTIVA")];
}
