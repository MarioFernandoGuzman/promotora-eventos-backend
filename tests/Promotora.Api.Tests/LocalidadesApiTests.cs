using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Venues;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Catalog.Persistence;

namespace Promotora.Api.Tests;

public sealed partial class VenuesApiTests
{
    [Fact]
    public async Task LocalidadesValidanGeometriaColorDuplicadosPropiedadYCuposExistentes()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct);
        var other = await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await LoginAsync(client, owner, ct);
        var created = await client.PostAsJsonAsync("/api/v1/recintos", new { nombre = "Con mapa", capacidadTotal = 100 }, ct);
        var recinto = (await created.Content.ReadFromJsonAsync<RecintoDto>(ct))!;
        string url = $"/api/v1/recintos/{recinto.Id}/localidades";
        var request = new DatosLocalidadDto("Palco", 60, new PoligonoDto("polygon", [new[] { 10d, 20d }, new[] { 80d, 20d }, new[] { 80d, 60d }, new[] { 10d, 60d }]), "#3366cc");
        var response = await client.PostAsJsonAsync(url, request, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var localidad = (await response.Content.ReadFromJsonAsync<LocalidadDto>(ct))!;
        Assert.Equal("#3366CC", localidad.Color);
        Assert.Equal(4, localidad.Geometria!.Points.Count);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{url}/{localidad.Id}", ct)).StatusCode);
        Assert.Single((await client.GetFromJsonAsync<LocalidadesPaginaDto>($"{url}?pageSize=1", ct))!.Items);
        await ProblemAsync(await client.PostAsJsonAsync(url, request with { Capacidad = 10 }, ct), 409, ct);
        await ProblemAsync(await client.PostAsJsonAsync(url, request with { Nombre = "Exceso", Capacidad = 41 }, ct), 422, ct);
        await ProblemAsync(await client.PostAsJsonAsync(url, request with { Color = "red" }, ct), 400, ct);
        await ProblemAsync(await client.PostAsJsonAsync(url, request with { Geometria = new PoligonoDto("polygon", [new[] { 1d, 2d }]) }, ct), 400, ct);
        await ProblemAsync(await client.PutAsJsonAsync($"/api/v1/recintos/{recinto.Id}", new { nombre = "Menor", capacidadTotal = 50 }, ct), 422, ct);
        await LoginAsync(client, other, ct);
        await ProblemAsync(await client.GetAsync(url, ct), 403, ct);
        await ProblemAsync(await client.PostAsJsonAsync(url, request, ct), 403, ct);
        await ProblemAsync(await client.PutAsJsonAsync($"{url}/{localidad.Id}", request, ct), 403, ct);
        await ProblemAsync(await client.DeleteAsync($"{url}/{localidad.Id}", ct), 403, ct);
        await LoginAsync(client, owner, ct);
        await using var db = fixture.CreateContext();
        var evento = new EventoRow
        {
            RecintoId = recinto.Id,
            CategoriaId = await db.CategoriasEvento.Select(x => x.Id).FirstAsync(ct),
            CreadoPor = owner.Id,
            Nombre = "Evento de prueba",
            FechaInicio = DateTime.UtcNow.AddDays(10),
            Estado = "BORRADOR",
            FechaCreacion = DateTime.UtcNow
        };
        db.Eventos.Add(evento);
        await db.SaveChangesAsync(ct);
        db.TiposEntrada.Add(new TipoEntradaRow { EventoId = evento.Id, LocalidadId = localidad.Id, Nombre = "Palco", Precio = 10, CupoTotal = 55, CupoDisponible = 55 });
        await db.SaveChangesAsync(ct);
        await ProblemAsync(await client.PutAsJsonAsync($"{url}/{localidad.Id}", request with { Capacidad = 50 }, ct), 422, ct);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"{url}/{localidad.Id}", request with { Nombre = "Palco nuevo", Capacidad = 55, Geometria = null, Color = null }, ct)).StatusCode);
        Assert.Null((await client.GetFromJsonAsync<LocalidadDto>($"{url}/{localidad.Id}", ct))!.Geometria);
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"{url}/{localidad.Id}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"{url}/{localidad.Id}", ct)).StatusCode);
        await ProblemAsync(await client.PutAsJsonAsync($"{url}/{localidad.Id}", request, ct), 409, ct);
        await ProblemAsync(await client.PostAsJsonAsync(url, request with { Nombre = "Todavía excede", Capacidad = 46 }, ct), 422, ct);
        Assert.False((await db.Localidades.AsNoTracking().SingleAsync(x => x.Id == localidad.Id, ct)).Activo);
        Assert.Equal(1, await db.LogsAuditoria.CountAsync(x => x.EntidadAfectada == "localidades" && x.EntidadId == localidad.Id.ToString() && x.Accion == "DELETE", ct));
        var openApi = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json", ct);
        Assert.True(openApi.GetProperty("paths").GetProperty("/api/v1/recintos/{recintoId}/localidades").GetProperty("post").TryGetProperty("security", out _));
    }

    [Fact]
    public async Task DosActoresCreandoLocalidadesConcurrentesNoExcedenLaCapacidad()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct);
        var admin = await CreateUserAsync(RolUsuario.ADMINISTRADOR, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var ownerClient = factory.CreateClient();
        using var adminClient = factory.CreateClient();
        await LoginAsync(ownerClient, owner, ct);
        await LoginAsync(adminClient, admin, ct);
        var created = await ownerClient.PostAsJsonAsync("/api/v1/recintos", new { nombre = "Carrera", capacidadTotal = 100 }, ct);
        var recinto = (await created.Content.ReadFromJsonAsync<RecintoDto>(ct))!;
        string url = $"/api/v1/recintos/{recinto.Id}/localidades";
        var responses = await Task.WhenAll(ownerClient.PostAsJsonAsync(url, new { nombre = "Zona A", capacidad = 60 }, ct),
            adminClient.PostAsJsonAsync(url, new { nombre = "Zona B", capacidad = 60 }, ct));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.UnprocessableEntity);
        foreach (var response in responses)
        {
            response.Dispose();
        }

        await using var db = fixture.CreateContext();
        Assert.Equal(60, await db.Localidades.Where(x => x.RecintoId == recinto.Id).SumAsync(x => x.Capacidad, ct));
        var ids = await db.Localidades.Where(x => x.RecintoId == recinto.Id).Select(x => x.Id.ToString()).ToListAsync(ct);
        Assert.Equal(1, await db.LogsAuditoria.CountAsync(x => x.EntidadAfectada == "localidades" && ids.Contains(x.EntidadId!) && x.Accion == "CREATE", ct));
    }
}
