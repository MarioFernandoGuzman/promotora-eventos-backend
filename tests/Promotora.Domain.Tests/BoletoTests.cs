using Promotora.Domain.Finance;
using Promotora.Domain.Ticketing;

namespace Promotora.Domain.Tests;

public sealed class BoletoTests
{
    private static readonly DateTime Ahora = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(null, "Documento")]
    [InlineData("Nombre", null)]
    [InlineData(" ", " ")]
    public void CarreraExigeNombreYDocumento(string? nombre, string? documento)
        => Assert.True(Boleto.Crear(Guid.NewGuid(), 1, "firma-de-prueba", new(nombre, documento), true, Ahora).IsFailure);

    [Fact]
    public void CarreraNormalizaAsistenteYConservaJsonYCantidadPorBoleto()
    {
        var creado = Boleto.Crear(Guid.NewGuid(), 1, "firma-de-prueba", new(" Nombre ", " Documento ", "{\"talla\":\"M\"}"), true, Ahora);
        Assert.True(creado.IsSuccess); Assert.Equal("Nombre", creado.Value.Asistente.NombreAsistente);
        Assert.Equal("Documento", creado.Value.Asistente.DocumentoIdentidad); Assert.Equal("VALIDO", creado.Value.Estado);
        Assert.Equal("{\"talla\":\"M\"}", creado.Value.Asistente.DatosExtraJson);
        Assert.True(Boleto.Crear(Guid.NewGuid(), 1, "firma-de-prueba", null, false, Ahora).IsSuccess);
    }

    [Fact]
    public void RechazaJsonFechaIdentificadorOTokenInvalidos()
    {
        Assert.True(Boleto.Crear(Guid.NewGuid(), 1, "firma", new(null, null, "{mal}"), false, Ahora).IsFailure);
        Assert.True(Boleto.Crear(Guid.Empty, 1, "firma", null, false, Ahora).IsFailure);
        Assert.True(Boleto.Crear(Guid.NewGuid(), 0, "firma", null, false, Ahora).IsFailure);
        Assert.True(Boleto.Crear(Guid.NewGuid(), 1, new string('a', 101), null, false, Ahora).IsFailure);
        Assert.True(Boleto.Crear(Guid.NewGuid(), 1, "firma", null, false, DateTime.SpecifyKind(Ahora, DateTimeKind.Unspecified)).IsFailure);
    }

    [Fact]
    public void RespaldoDebeCuadrarPorComponenteCuentaYPago()
    {
        var importes = ImportesCompra.DesdeMontos(300m, 30m, 39.60m, 369.60m).Value;
        var respaldo = new RespaldoCobro(true, true, true, 3, 300m, 30m, 39.60m);
        Assert.True(respaldo.PermiteEmision(importes));
        Assert.False((respaldo with { Venta = 301m, Comision = 29m }).PermiteEmision(importes));
        Assert.False((respaldo with { PagoAprobado = false }).PermiteEmision(importes));
        Assert.False((respaldo with { CuentasCorrectas = false }).PermiteEmision(importes));
        Assert.False((respaldo with { SoloCobrosPositivos = false }).PermiteEmision(importes));
        Assert.False((respaldo with { CantidadMovimientos = 4 }).PermiteEmision(importes));
        var cero = ImportesCompra.DesdeMontos(0m, 0m, 0m, 0m).Value;
        Assert.True(new RespaldoCobro(false, true, true, 0, 0m, 0m, 0m).PermiteEmision(cero));
        Assert.False(new RespaldoCobro(false, true, true, 1, 0m, 0m, 0m).PermiteEmision(cero));
    }
}
