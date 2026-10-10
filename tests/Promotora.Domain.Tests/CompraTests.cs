using System.Globalization;
using Promotora.Domain.Finance;
using Promotora.Domain.Shared;
using Promotora.Domain.Ticketing;

namespace Promotora.Domain.Tests;

public sealed class CompraTests
{
    private static readonly DateTime Ahora = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly PoliticaTarifaria Politica = new(0.12m, 0.10m);
    private static decimal Decimal(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);
    private static Compra Nueva() => Compra.Crear(Guid.NewGuid(), 1, 2, Ahora, Ahora.AddMinutes(5), "clave", "GTQ",
        [new EntradaCompra(3, 2, 2, 150m)], 5, Politica).Value;

    [Theory]
    [InlineData("300.00", "30.00", "39.60", "369.60")]
    [InlineData("33.33", "3.33", "4.40", "41.06")]
    [InlineData("0.00", "0.00", "0.00", "0.00")]
    [InlineData("0.05", "0.01", "0.01", "0.07")]
    public void D15RedondeaComponentesYCalculaIvaSobreSubtotalMasComision(string subtotal, string comision, string iva, string total)
    {
        var result = ImportesCompra.Calcular(Decimal(subtotal), Politica);
        Assert.True(result.IsSuccess);
        Assert.Equal(Decimal(comision), result.Value.ComisionPlataforma);
        Assert.Equal(Decimal(iva), result.Value.Impuestos);
        Assert.Equal(Decimal(total), result.Value.Total);
        Assert.Equal(Decimal(total) > 0, result.Value.RequiereMovimientos);
    }

    [Fact]
    public void SerieAleatoriaMantieneInvarianteDeImportesYPrecision()
    {
        var random = new Random(20261010);
        for (int i = 0; i < 2000; i++)
        {
            decimal subtotal = random.NextInt64(0, 1000000000) / 100m;
            var importes = ImportesCompra.Calcular(subtotal, Politica).Value;
            Assert.Equal(subtotal + importes.ComisionPlataforma + importes.Impuestos, importes.Total);
            Assert.Equal(decimal.Round(importes.Total, 2), importes.Total);
            Assert.Equal(decimal.Round(importes.ComisionPlataforma, 2), importes.ComisionPlataforma);
            Assert.Equal(decimal.Round(importes.Impuestos, 2), importes.Impuestos);
        }
    }

    [Fact]
    public void RedondeaANivelDeCompraYNoPorLinea()
    {
        var compra = Compra.Crear(Guid.NewGuid(), 1, 2, Ahora, Ahora.AddMinutes(5), "clave", "GTQ",
            [new EntradaCompra(3, 2, 1, 0.03m), new EntradaCompra(4, 2, 1, 0.03m)], 5, Politica).Value;
        Assert.Equal(0.06m, compra.Importes.Subtotal);
        Assert.Equal(0.01m, compra.Importes.ComisionPlataforma);
        Assert.Equal(0.01m, compra.Importes.Impuestos);
        Assert.Equal(0.08m, compra.Importes.Total);
    }

    [Fact]
    public void RechazaPrecisionNegativosDesbordamientoYTasasNegativas()
    {
        foreach (decimal subtotal in new[] { -1m, 1.001m, 100000000m, ImportesCompra.Maximo })
            Assert.True(ImportesCompra.Calcular(subtotal, Politica).IsFailure);
        Assert.True(ImportesCompra.Calcular(1, new(-0.12m, 0.10m)).IsFailure);
        Assert.True(ImportesCompra.Calcular(1, new(0.12m, -0.10m)).IsFailure);
        Assert.True(ImportesCompra.Calcular(300, new(decimal.MaxValue, decimal.MaxValue)).IsFailure);
        Assert.True(ImportesCompra.DesdeMontos(300, 30, 39.60m, 369.61m).IsFailure);
        Assert.True(ImportesCompra.Calcular(ImportesCompra.Maximo, new(0, 0)).IsSuccess);
        Assert.Equal(0.01m, ImportesCompra.Calcular(0.05m, new(0.10m, 0)).Value.Impuestos);
    }

    [Fact]
    public void CongelaPrecioYComponentesPorCompraYSumaLineasAntesDeAplicarTasas()
    {
        var entradas = new List<EntradaCompra> { new(3, 2, 2, 100m), new(4, 2, 1, 100m) };
        var compra = Compra.Crear(Guid.NewGuid(), 1, 2, Ahora, Ahora.AddMinutes(5), "clave", "GTQ", entradas, 5, Politica).Value;
        entradas[0] = entradas[0] with { PrecioUnitario = 1000m }; entradas.Clear();
        Assert.Equal(2, compra.Detalles.Count); Assert.Equal(100m, compra.Detalles[0].PrecioUnitario);
        Assert.Equal(200m, compra.Detalles[0].Subtotal); Assert.Equal(369.60m, compra.Importes.Total);
        Assert.True(((ICollection<DetalleCompra>)compra.Detalles).IsReadOnly);
        var congelados = ImportesCompra.DesdeMontos(compra.Importes.Subtotal, compra.Importes.ComisionPlataforma, compra.Importes.Impuestos, compra.Importes.Total).Value;
        Assert.Equal(compra.Importes, congelados);
        Assert.NotEqual(congelados.Total, ImportesCompra.Calcular(300, new(0.15m, 0.20m)).Value.Total);
    }

    [Fact]
    public void RechazaEventoAjenoCantidadInvalidaYLimiteAgregado()
    {
        foreach (var entradas in new EntradaCompra[][] {
            [], [new(3, 4, 1, 10)], [new(3, 2, 0, 10)], [new(3, 2, -1, 10)],
            [new(3, 2, 3, 10), new(4, 2, 3, 10)], [new(0, 2, 1, 10)], [new(3, 2, 1, 10.001m)],
            [new(3, 2, int.MaxValue, 10)], [new(3, 2, 2, ImportesCompra.Maximo)] })
            Assert.True(Compra.Crear(Guid.NewGuid(), 1, 2, Ahora, Ahora.AddMinutes(5), "clave", "GTQ", entradas, 5, Politica).IsFailure);
        Assert.True(Compra.Crear(Guid.Empty, 1, 2, Ahora, Ahora.AddMinutes(5), "clave", "GTQ", [new(3, 2, 1, 10)], 5, Politica).IsFailure);
        Assert.True(Compra.Crear(Guid.NewGuid(), 1, 2, Ahora, Ahora, "clave", "GTQ", [new(3, 2, 1, 10)], 5, Politica).IsFailure);
        Assert.True(Compra.Crear(Guid.NewGuid(), 1, 2, DateTime.SpecifyKind(Ahora, DateTimeKind.Unspecified), Ahora.AddMinutes(5), "clave", "GTQ", [new(3, 2, 1, 10)], 5, Politica).IsFailure);
        Assert.True(Compra.Crear(Guid.NewGuid(), 1, 2, Ahora, Ahora.AddMinutes(5), new string('a', 101), "GTQ", [new(3, 2, 1, 10)], 5, Politica).IsFailure);
    }

    [Theory]
    [InlineData(EstadoCompra.COMPLETADA)]
    [InlineData(EstadoCompra.FALLIDA)]
    public void ProcesamientoRegistraFechaYEstadosFinalesSonIrreversibles(EstadoCompra final)
    {
        var compra = Nueva();
        Assert.True(compra.CambiarEstado(EstadoCompra.PENDIENTE, EstadoCompra.PROCESANDO, Ahora.AddSeconds(1)).IsSuccess);
        Assert.Equal(Ahora.AddSeconds(1), compra.ProcesandoDesde);
        Assert.Equal(ErrorType.Conflict, Assert.Single(compra.CambiarEstado(EstadoCompra.PENDIENTE, EstadoCompra.CANCELADA, Ahora.AddSeconds(2)).Errors).Type);
        Assert.True(compra.CambiarEstado(EstadoCompra.PROCESANDO, EstadoCompra.CANCELADA, Ahora.AddSeconds(2)).IsFailure);
        Assert.True(compra.CambiarEstado(EstadoCompra.PROCESANDO, final, Ahora.AddSeconds(2)).IsSuccess);
        foreach (var siguiente in Enum.GetValues<EstadoCompra>())
            Assert.True(compra.CambiarEstado(final, siguiente, Ahora.AddSeconds(3)).IsFailure);
        Assert.Equal(369.60m, compra.Importes.Total);
    }

    [Fact]
    public void ExpiracionSoloPendienteYDesdeFechaLimiteCancelacionSoloPendiente()
    {
        var compra = Nueva();
        Assert.True(compra.CambiarEstado(EstadoCompra.PENDIENTE, EstadoCompra.EXPIRADA, Ahora.AddMinutes(4)).IsFailure);
        Assert.True(compra.CambiarEstado(EstadoCompra.PENDIENTE, EstadoCompra.EXPIRADA, compra.ExpiraEn).IsSuccess);
        Assert.Null(compra.ProcesandoDesde);
        Assert.True(compra.CambiarEstado(EstadoCompra.EXPIRADA, EstadoCompra.PROCESANDO, compra.ExpiraEn).IsFailure);
        var cancelada = Nueva();
        Assert.True(cancelada.CambiarEstado(EstadoCompra.PENDIENTE, EstadoCompra.CANCELADA, Ahora).IsSuccess);
        Assert.True(cancelada.CambiarEstado(EstadoCompra.CANCELADA, EstadoCompra.CANCELADA, Ahora).IsFailure);
        var procesando = Nueva();
        Assert.True(procesando.CambiarEstado(EstadoCompra.PENDIENTE, EstadoCompra.COMPLETADA, Ahora).IsFailure);
        Assert.True(procesando.CambiarEstado(EstadoCompra.PENDIENTE, EstadoCompra.PROCESANDO, Ahora).IsSuccess);
        Assert.True(procesando.CambiarEstado(EstadoCompra.PROCESANDO, EstadoCompra.EXPIRADA, compra.ExpiraEn).IsFailure);
    }
}
