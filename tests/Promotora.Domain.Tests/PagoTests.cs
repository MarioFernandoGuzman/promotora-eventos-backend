using Promotora.Domain.Finance;

namespace Promotora.Domain.Tests;

public sealed class PagoTests
{
    [Theory]
    [InlineData("APROBADO")]
    [InlineData("RECHAZADO")]
    public void ResultadoEsIdempotentePeroNoPermiteCambiarUnEstadoFinal(string estado)
    {
        var referencia = Guid.NewGuid(); var pago = new Pago(referencia, 123.20m, "GTQ", "SIMULADA", "PENDIENTE");
        var fecha = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.True(pago.Resolver(referencia, 123.20m, "GTQ", "SIMULADA", estado, fecha).IsSuccess);
        Assert.True(pago.Resolver(referencia, 123.20m, "GTQ", "SIMULADA", estado, fecha.AddMinutes(1)).IsSuccess);
        Assert.True(pago.Resolver(referencia, 123.20m, "GTQ", "SIMULADA", estado == "APROBADO" ? "RECHAZADO" : "APROBADO", fecha).IsFailure);
        Assert.Equal(estado, pago.Estado);
    }

    [Theory]
    [InlineData("referencia")]
    [InlineData("monto")]
    [InlineData("moneda")]
    [InlineData("pasarela")]
    [InlineData("fecha")]
    [InlineData("estado")]
    public void RechazaRespuestaIncompatibleSinMutarElIntento(string campo)
    {
        var referencia = Guid.NewGuid(); var pago = new Pago(referencia, 10m, "GTQ", "SIMULADA", "PENDIENTE");
        var fecha = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = pago.Resolver(campo == "referencia" ? Guid.NewGuid() : referencia, campo == "monto" ? 11m : 10m,
            campo == "moneda" ? "USD" : "GTQ", campo == "pasarela" ? "OTRA" : "SIMULADA", campo == "estado" ? "REEMBOLSADO" : "APROBADO",
            campo == "fecha" ? DateTime.SpecifyKind(fecha, DateTimeKind.Unspecified) : fecha);
        Assert.True(result.IsFailure); Assert.Equal("PENDIENTE", pago.Estado);
    }
}
