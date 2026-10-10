using Promotora.Domain.Catalog;

namespace Promotora.Domain.Tests;

public sealed class EventoTests
{
    [Fact]
    public void VentaExigeVentanaCupoYEstadoEsperado()
    {
        var now = new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);
        var datos = new DatosEvento(1, 1, "Evento", null, now.AddDays(1), null, now.AddHours(-1), now.AddHours(1), 5, null);
        var evento = new Evento(1, 1, datos, EstadoEvento.BORRADOR, now);
        Assert.True(evento.CambiarEstado(EstadoEvento.BORRADOR, EstadoEvento.EN_VENTA, now, 10).IsFailure);
        Assert.True(evento.CambiarEstado(EstadoEvento.BORRADOR, EstadoEvento.PROGRAMADO, now, 10).IsSuccess);
        Assert.True(evento.CambiarEstado(EstadoEvento.PROGRAMADO, EstadoEvento.EN_VENTA, now, 0).IsFailure);
        Assert.True(evento.CambiarEstado(EstadoEvento.PROGRAMADO, EstadoEvento.EN_VENTA, now.AddHours(1), 10).IsFailure);
        Assert.True(evento.CambiarEstado(EstadoEvento.PROGRAMADO, EstadoEvento.EN_VENTA, now, 10).IsSuccess);
        Assert.True(evento.CambiarEstado(EstadoEvento.PROGRAMADO, EstadoEvento.CANCELADO, now, 10).IsFailure);
        Assert.True(evento.CambiarEstado(EstadoEvento.EN_VENTA, EstadoEvento.FINALIZADO, now, 10).IsFailure);
        Assert.True(evento.CambiarEstado(EstadoEvento.EN_VENTA, EstadoEvento.AGOTADO, now, 0).IsSuccess);
    }

    [Fact]
    public void ConfiguracionNoTrasladaRecintoNiModificaEventoEnVenta()
    {
        var now = new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);
        var datos = new DatosEvento(1, 1, "Evento", null, now, null, null, null, 5, null);
        var evento = new Evento(1, 1, datos, EstadoEvento.BORRADOR, now);
        Assert.True(evento.Editar(datos with { RecintoId = 2 }).IsFailure);
        Assert.True(evento.Editar(datos with { Nombre = "Nuevo" }).IsSuccess);
        Assert.Throws<ArgumentException>(() => new Evento(1, 1, datos with { MaxBoletosPorCompra = 0 }, EstadoEvento.BORRADOR, now));
    }
}
