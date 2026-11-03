using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Catalog;
using Promotora.Application.Shared;
using Promotora.Domain.Identity;
using Promotora.WebApi.Catalog;

namespace Promotora.Api.Tests;

public sealed partial class CatalogApiTests
{
    [Fact]
    public async Task TiposEntradaValidanLocalidadPrecioCupoPropiedadYConservanHistoria()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var owner = factory.CreateClient();
        using var other = factory.CreateClient();
        await LoginAsync(owner, await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct), ct);
        await LoginAsync(other, await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct), ct);
        int recinto = await CrearRecintoAsync(owner, ct);
        int otroRecinto = await CrearRecintoAsync(other, ct);
        var localidad = await PostAsync<Promotora.Application.Venues.LocalidadDto>(owner, $"/api/v1/recintos/{recinto}/localidades", new { nombre = "VIP", capacidad = 100 }, ct);
        var ajena = await PostAsync<Promotora.Application.Venues.LocalidadDto>(other, $"/api/v1/recintos/{otroRecinto}/localidades", new { nombre = "Ajena", capacidad = 100 }, ct);
        var evento = await PostAsync<EventoDto>(owner, "/api/v1/eventos", DatosEvento(recinto), ct);
        string url = $"/api/v1/eventos/{evento.Id}/tipos-entrada";
        var tipo = await PostAsync<TipoEntradaDto>(owner, url, new CrearTipoEntradaRequest("VIP", 50, 60, localidad.Id), ct);
        Assert.Equal(60, tipo.CupoDisponible);
        await ProblemAsync(await owner.PostAsJsonAsync(url, new CrearTipoEntradaRequest("Exceso", 10, 41, localidad.Id), ct), 422, ct);
        await ProblemAsync(await owner.PostAsJsonAsync(url, new CrearTipoEntradaRequest("Ajena", 10, 1, ajena.Id), ct), 404, ct);
        await ProblemAsync(await owner.PostAsJsonAsync(url, new CrearTipoEntradaRequest("Inválido", 1.001m, 1), ct), 400, ct);
        await ProblemAsync(await owner.PostAsJsonAsync(url, new CrearTipoEntradaRequest("Inválido", -1, 1), ct), 400, ct);
        await ProblemAsync(await owner.PostAsJsonAsync(url, new CrearTipoEntradaRequest("Inválido", 100000000, 1), ct), 400, ct);
        await ProblemAsync(await owner.PostAsJsonAsync(url, new CrearTipoEntradaRequest("Inválido", 1, -1), ct), 400, ct);
        await ProblemAsync(await other.GetAsync(url, ct), 403, ct);
        await ProblemAsync(await other.PutAsJsonAsync($"{url}/{tipo.Id}", new EditarTipoEntradaRequest("Ajena", 1, 1), ct), 403, ct);
        using var edit = await owner.PutAsJsonAsync($"{url}/{tipo.Id}", new EditarTipoEntradaRequest("VIP reducida", 75.25m, 40), ct);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal(40, (await edit.Content.ReadFromJsonAsync<TipoEntradaDto>(ct))!.CupoDisponible);
        Assert.Equal(HttpStatusCode.OK, (await owner.DeleteAsync($"{url}/{tipo.Id}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.DeleteAsync($"{url}/{tipo.Id}", ct)).StatusCode);
        await ProblemAsync(await owner.PostAsJsonAsync(url, new CrearTipoEntradaRequest("Exceso con inactiva", 10, 61, localidad.Id), ct), 422, ct);
        Assert.False((await owner.GetFromJsonAsync<TipoEntradaDto>($"{url}/{tipo.Id}", ct))!.Activo);
        var carrera = await PostAsync<TipoEntradaDto>(owner, url, new CrearTipoEntradaRequest("10K", 0, 10), ct);
        Assert.Null(carrera.LocalidadId);
        var vacia = await PostAsync<TipoEntradaDto>(owner, url, new CrearTipoEntradaRequest("General sin cupo", 0, 0), ct);
        Assert.Equal(0, vacia.CupoDisponible);
        Assert.Equal(3, (await owner.GetFromJsonAsync<PaginaDto<TipoEntradaDto>>(url, ct))!.Total);
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync($"/api/v1/eventos/{evento.Id}/estado", new EstadoEventoRequest("BORRADOR", "PROGRAMADO"), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync($"/api/v1/eventos/{evento.Id}/estado", new EstadoEventoRequest("PROGRAMADO", "EN_VENTA"), ct)).StatusCode);
        await ProblemAsync(await owner.PutAsJsonAsync($"{url}/{carrera.Id}", new EditarTipoEntradaRequest("10K", 10, 20), ct), 409, ct);
        await ProblemAsync(await owner.DeleteAsync($"{url}/{carrera.Id}", ct), 409, ct);
        await using var db = fixture.CreateContext();
        Assert.Equal(1, await db.LogsAuditoria.CountAsync(x => x.EntidadAfectada == "tipos_entrada" && x.EntidadId == tipo.Id.ToString() && x.Accion == "DELETE", ct));
        Assert.True(await db.TiposEntrada.AnyAsync(x => x.Id == tipo.Id && !x.Activo && x.CupoTotal == 40 && x.CupoDisponible == 40, ct));
    }

    [Fact]
    public async Task ConfiguracionesConcurrentesNoExcedenCapacidadDeLocalidad()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var owner = factory.CreateClient(); using var admin = factory.CreateClient();
        await LoginAsync(owner, await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct), ct);
        await LoginAsync(admin, await CreateUserAsync(RolUsuario.ADMINISTRADOR, ct), ct);
        int recinto = await CrearRecintoAsync(owner, ct);
        var localidad = await PostAsync<Promotora.Application.Venues.LocalidadDto>(owner, $"/api/v1/recintos/{recinto}/localidades", new { nombre = "Zona", capacidad = 100 }, ct);
        var evento = await PostAsync<EventoDto>(owner, "/api/v1/eventos", DatosEvento(recinto), ct);
        string url = $"/api/v1/eventos/{evento.Id}/tipos-entrada";
        var results = await Task.WhenAll(owner.PostAsJsonAsync(url, new CrearTipoEntradaRequest("A", 1, 60, localidad.Id), ct),
            admin.PostAsJsonAsync(url, new CrearTipoEntradaRequest("B", 1, 60, localidad.Id), ct));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.UnprocessableEntity);
        await using var db = fixture.CreateContext();
        Assert.Equal(60, await db.TiposEntrada.Where(x => x.EventoId == evento.Id).SumAsync(x => x.CupoTotal, ct));
        Assert.Equal(1, await db.LogsAuditoria.CountAsync(x => x.EntidadAfectada == "tipos_entrada" && x.Accion == "CREATE"
            && db.TiposEntrada.Any(t => t.EventoId == evento.Id && t.Id.ToString() == x.EntidadId), ct));
    }

    [Fact]
    public async Task AjusteDeCupoNoRestauraReservasExistentes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var owner = factory.CreateClient();
        await LoginAsync(owner, await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct), ct);
        var evento = await PostAsync<EventoDto>(owner, "/api/v1/eventos", DatosEvento(await CrearRecintoAsync(owner, ct)), ct);
        string url = $"/api/v1/eventos/{evento.Id}/tipos-entrada";
        var tipo = await PostAsync<TipoEntradaDto>(owner, url, new CrearTipoEntradaRequest("General", 10, 10), ct);
        await using var db = fixture.CreateContext();
        Assert.Equal(1, await db.TiposEntrada.Where(x => x.Id == tipo.Id && x.CupoDisponible >= 1).ExecuteUpdateAsync(s => s.SetProperty(x => x.CupoDisponible, x => x.CupoDisponible - 1), ct));
        await ProblemAsync(await owner.PutAsJsonAsync($"{url}/{tipo.Id}", new EditarTipoEntradaRequest("General", 10, 20), ct), 409, ct);
        await ProblemAsync(await owner.DeleteAsync($"{url}/{tipo.Id}", ct), 409, ct);
        Assert.Equal(9, (await owner.GetFromJsonAsync<TipoEntradaDto>($"{url}/{tipo.Id}", ct))!.CupoDisponible);
    }
}
