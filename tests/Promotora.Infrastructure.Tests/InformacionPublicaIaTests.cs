using Microsoft.EntityFrameworkCore;
using Promotora.Infrastructure.AI.Persistence;
using Promotora.Infrastructure.Shared;

namespace Promotora.Infrastructure.Tests;

public sealed class InformacionPublicaIaTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task SoloPublicaEventosEnVentaConRecintoActivoYNoExponeDatosPrivados()
    {
        var ct = TestContext.Current.CancellationToken; await using var db = postgres.CreateContext();
        var datos = await CompraFixture.CrearAsync(db, 1, ct);
        await db.Eventos.Where(e => e.Id == datos.EventoId).ExecuteUpdateAsync(s => s.SetProperty(e => e.Estado, "EN_VENTA")
            .SetProperty(e => e.FechaInicio, DateTime.UtcNow.AddDays(5)).SetProperty(e => e.FechaInicioVenta, (DateTime?)null).SetProperty(e => e.FechaFinVenta, (DateTime?)null), ct);
        var puerto = new EfInformacionPublicaPort(db, new SystemClock(TimeProvider.System));
        var fuente = Assert.Single(await puerto.BuscarAsync("¿Qué eventos hay?", ct));
        Assert.Single(await puerto.BuscarAsync(fuente.Documento, ct));
        Assert.Empty(await puerto.BuscarAsync("ContenidoInexistenteParaBusqueda", ct));
        Assert.StartsWith("/api/v1/catalogo/eventos/", fuente.Url); Assert.Contains("Precios base", fuente.Contenido);
        Assert.DoesNotContain("cliente_id", fuente.Contenido); Assert.DoesNotContain("password", fuente.Contenido);
        await db.Eventos.Where(e => e.Id == datos.EventoId).ExecuteUpdateAsync(s => s.SetProperty(e => e.Estado, "BORRADOR"), ct);
        Assert.Empty(await puerto.BuscarAsync("¿Qué eventos hay?", ct));
    }
}
