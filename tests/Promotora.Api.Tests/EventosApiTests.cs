using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Catalog;
using Promotora.Application.Shared;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Catalog.Persistence;
using Promotora.WebApi.Catalog;

namespace Promotora.Api.Tests;

public sealed partial class CatalogApiTests
{
    private static async Task<int> CrearRecintoAsync(HttpClient client, CancellationToken ct)
    {
        var recinto = await PostAsync<JsonElement>(client, "/api/v1/recintos", new { nombre = "Recinto catálogo", ciudad = "Guatemala", capacidadTotal = 100 }, ct);
        return recinto.GetProperty("id").GetInt32();
    }

    private static EventoRequest DatosEvento(int recintoId) => new(recintoId, 1, "Evento catálogo",
        new DateTime(2027, 6, 1, 18, 0, 0, DateTimeKind.Utc), FechaInicioVenta: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        FechaFinVenta: new DateTime(2090, 1, 1, 0, 0, 0, DateTimeKind.Utc), MaxBoletosPorCompra: 5);

    [Fact]
    public async Task EventosRespetanCreadorPropiedadVentanaEstadosYCancellationLogica()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await LoginAsync(client, owner, ct);
        int recinto = await CrearRecintoAsync(client, ct);
        var datos = DatosEvento(recinto);
        var evento = await PostAsync<EventoDto>(client, "/api/v1/eventos", datos, ct);
        Assert.Equal(owner.Id, evento.CreadoPor);
        Assert.Equal("BORRADOR", evento.Estado);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/v1/eventos/{evento.Id}", datos with { Nombre = "Editado" }, ct)).StatusCode);
        await ProblemAsync(await client.PostAsJsonAsync("/api/v1/eventos", datos with { MaxBoletosPorCompra = 0 }, ct), 400, ct);
        await ProblemAsync(await client.PostAsJsonAsync("/api/v1/eventos", datos with { FechaFin = datos.FechaInicio.AddMinutes(-1) }, ct), 400, ct);
        await ProblemAsync(await client.PostAsJsonAsync("/api/v1/eventos", datos with { FechaInicio = DateTime.SpecifyKind(datos.FechaInicio, DateTimeKind.Unspecified) }, ct), 400, ct);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/v1/eventos/{evento.Id}/estado", new EstadoEventoRequest("BORRADOR", "PROGRAMADO"), ct)).StatusCode);
        await ProblemAsync(await client.PutAsJsonAsync($"/api/v1/eventos/{evento.Id}/estado", new EstadoEventoRequest("BORRADOR", "PROGRAMADO"), ct), 409, ct);
        await ProblemAsync(await client.PutAsJsonAsync($"/api/v1/eventos/{evento.Id}/estado", new EstadoEventoRequest("PROGRAMADO", "EN_VENTA"), ct), 422, ct);
        await using (var db = fixture.CreateContext())
        {
            db.TiposEntrada.Add(new TipoEntradaRow { EventoId = evento.Id, Nombre = "General", Precio = 10, CupoTotal = 10, CupoDisponible = 10 });
            await db.SaveChangesAsync(ct);
        }
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/v1/eventos/{evento.Id}/estado", new EstadoEventoRequest("PROGRAMADO", "EN_VENTA"), ct)).StatusCode);
        await ProblemAsync(await client.PutAsJsonAsync($"/api/v1/eventos/{evento.Id}/estado", new EstadoEventoRequest("EN_VENTA", "AGOTADO"), ct), 422, ct);
        await ProblemAsync(await client.PutAsJsonAsync($"/api/v1/eventos/{evento.Id}", datos, ct), 409, ct);
        using var other = factory.CreateClient();
        await LoginAsync(other, await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct), ct);
        await ProblemAsync(await other.GetAsync($"/api/v1/eventos/{evento.Id}", ct), 403, ct);
        await ProblemAsync(await other.DeleteAsync($"/api/v1/eventos/{evento.Id}", ct), 403, ct);
        Assert.Empty((await other.GetFromJsonAsync<PaginaDto<EventoDto>>("/api/v1/eventos", ct))!.Items);
        using var customer = factory.CreateClient();
        await LoginAsync(customer, await CreateUserAsync(RolUsuario.CLIENTE, ct), ct);
        await ProblemAsync(await customer.GetAsync("/api/v1/eventos", ct), 403, ct);
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/v1/eventos/{evento.Id}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/v1/eventos/{evento.Id}", ct)).StatusCode);
        await using var check = fixture.CreateContext();
        Assert.Equal("CANCELADO", (await check.Eventos.SingleAsync(x => x.Id == evento.Id, ct)).Estado);
        Assert.Equal(1, await check.LogsAuditoria.CountAsync(x => x.EntidadAfectada == "eventos" && x.EntidadId == evento.Id.ToString() && x.Accion == "DELETE", ct));
    }

    [Fact]
    public async Task DosActoresCompitenPorUnaTransicionYSoloUnoGana()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var owner = factory.CreateClient();
        using var admin = factory.CreateClient();
        await LoginAsync(owner, await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct), ct);
        await LoginAsync(admin, await CreateUserAsync(RolUsuario.ADMINISTRADOR, ct), ct);
        var evento = await PostAsync<EventoDto>(owner, "/api/v1/eventos", DatosEvento(await CrearRecintoAsync(owner, ct)), ct);
        var results = await Task.WhenAll(owner.PutAsJsonAsync($"/api/v1/eventos/{evento.Id}/estado", new EstadoEventoRequest("BORRADOR", "PROGRAMADO"), ct),
            admin.PutAsJsonAsync($"/api/v1/eventos/{evento.Id}/estado", new EstadoEventoRequest("BORRADOR", "PROGRAMADO"), ct));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        await using var db = fixture.CreateContext();
        Assert.Equal(1, await db.LogsAuditoria.CountAsync(x => x.EntidadAfectada == "eventos" && x.EntidadId == evento.Id.ToString() && x.Accion == "UPDATE", ct));
    }

    [Fact]
    public async Task EventoConComprasNoSeEditaNiCancelaDesdeCrud()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var owner = factory.CreateClient();
        await LoginAsync(owner, await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct), ct);
        var datos = DatosEvento(await CrearRecintoAsync(owner, ct));
        var evento = await PostAsync<EventoDto>(owner, "/api/v1/eventos", datos, ct);
        var customer = await CreateUserAsync(RolUsuario.CLIENTE, ct);
        await using var db = fixture.CreateContext();
        int clienteId = (await db.Clientes.SingleAsync(x => x.UsuarioId == customer.Id, ct)).Id;
        db.Compras.Add(new Promotora.Infrastructure.Ticketing.Persistence.CompraRow
        {
            ClienteId = clienteId,
            EventoId = evento.Id,
            Estado = "PENDIENTE",
            ExpiraEn = new DateTime(2090, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            Moneda = "GTQ"
        });
        await db.SaveChangesAsync(ct);
        await ProblemAsync(await owner.PutAsJsonAsync($"/api/v1/eventos/{evento.Id}", datos, ct), 409, ct);
        await ProblemAsync(await owner.DeleteAsync($"/api/v1/eventos/{evento.Id}", ct), 409, ct);
        await ProblemAsync(await owner.PutAsJsonAsync($"/api/v1/eventos/{evento.Id}/estado", new EstadoEventoRequest("BORRADOR", "CANCELADO"), ct), 409, ct);
        Assert.Equal("BORRADOR", (await db.Eventos.AsNoTracking().SingleAsync(x => x.Id == evento.Id, ct)).Estado);
    }
}
