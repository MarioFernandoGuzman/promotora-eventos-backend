using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Catalog;
using Promotora.Application.Shared;
using Promotora.Domain.Identity;
using Promotora.WebApi.Catalog;

namespace Promotora.Api.Tests;

public sealed partial class CatalogApiTests
{
    [Fact]
    public async Task CatalogoAnonimoFiltraPaginaYOcultaDatosPrivadosEInactivos()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var owner = factory.CreateClient(); using var admin = factory.CreateClient(); using var publicClient = factory.CreateClient();
        await LoginAsync(owner, await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct), ct);
        await LoginAsync(admin, await CreateUserAsync(RolUsuario.ADMINISTRADOR, ct), ct);
        var category = await PostAsync<CategoriaEventoDto>(admin, "/api/v1/categorias", new { nombre = Guid.NewGuid().ToString("N") }, ct);
        int recinto = await CrearRecintoAsync(owner, ct);
        string texto = Guid.NewGuid().ToString("N");
        var datos = DatosEvento(recinto) with { CategoriaId = category.Id, Nombre = "Festival " + texto };
        var evento = await PostAsync<EventoDto>(owner, "/api/v1/eventos", datos, ct);
        var borrador = await PostAsync<EventoDto>(owner, "/api/v1/eventos", datos, ct);
        await ProblemAsync(await publicClient.GetAsync($"/api/v1/catalogo/eventos/{evento.Id}", ct), 404, ct);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/v1/eventos/{evento.Id}", ct)).StatusCode);
        await ProblemAsync(await publicClient.GetAsync($"/api/v1/eventos/{evento.Id}", ct), 401, ct);
        var presenter = await PostAsync<PresentadorDto>(admin, "/api/v1/presentadores", new { nombre = "Artista público", tipo = "ARTISTA" }, ct);
        await PostAsync<EventoPresentadorDto>(owner, $"/api/v1/eventos/{evento.Id}/presentadores", new { presentadorId = presenter.Id, rol = "HEADLINER" }, ct);
        var tipo = await PostAsync<TipoEntradaDto>(owner, $"/api/v1/eventos/{evento.Id}/tipos-entrada", new CrearTipoEntradaRequest("General", 25, 10), ct);
        var inactive = await PostAsync<TipoEntradaDto>(owner, $"/api/v1/eventos/{evento.Id}/tipos-entrada", new CrearTipoEntradaRequest("Oculta", 50, 10), ct);
        Assert.Equal(HttpStatusCode.OK, (await owner.DeleteAsync($"/api/v1/eventos/{evento.Id}/tipos-entrada/{inactive.Id}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync($"/api/v1/eventos/{evento.Id}/estado", new EstadoEventoRequest("BORRADOR", "PROGRAMADO"), ct)).StatusCode);
        string url = $"/api/v1/catalogo/eventos?categoriaId={category.Id}&ciudad=guatemala&desde=2027-06-01T18:00:00Z&hasta=2027-06-01T18:00:00Z&texto={texto}&page=1&pageSize=1";
        var page = (await publicClient.GetFromJsonAsync<PaginaDto<EventoResumenPublicoDto>>(url, ct))!;
        Assert.Equal(1, page.Total); Assert.Equal(evento.Id, Assert.Single(page.Items).Id);
        Assert.Empty((await publicClient.GetFromJsonAsync<PaginaDto<EventoResumenPublicoDto>>(url.Replace("page=1", "page=2"), ct))!.Items);
        Assert.Empty((await publicClient.GetFromJsonAsync<PaginaDto<EventoResumenPublicoDto>>(url.Replace("guatemala", "OtraCiudad"), ct))!.Items);
        var detail = (await publicClient.GetFromJsonAsync<EventoPublicoDto>($"/api/v1/catalogo/eventos/{evento.Id}", ct))!;
        Assert.Equal(presenter.Id, Assert.Single(detail.Presentadores).Id);
        Assert.Equal(tipo.Id, Assert.Single(detail.TiposEntrada).Id);
        Assert.Equal(25, detail.TiposEntrada[0].Precio);
        Assert.Equal(recinto, detail.Recinto.Id);
        var json = await publicClient.GetFromJsonAsync<JsonElement>($"/api/v1/catalogo/eventos/{evento.Id}", ct);
        Assert.False(json.TryGetProperty("creadoPor", out _));
        Assert.False(json.GetProperty("recinto").TryGetProperty("duenoId", out _));
        Assert.DoesNotContain("password", json.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("usuarioId", json.GetRawText(), StringComparison.OrdinalIgnoreCase);
        await ProblemAsync(await publicClient.GetAsync($"/api/v1/catalogo/eventos/{borrador.Id}", ct), 404, ct);
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/v1/presentadores/{presenter.Id}", ct)).StatusCode);
        Assert.Empty((await publicClient.GetFromJsonAsync<EventoPublicoDto>($"/api/v1/catalogo/eventos/{evento.Id}", ct))!.Presentadores);
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/v1/categorias/{category.Id}", ct)).StatusCode);
        await ProblemAsync(await publicClient.GetAsync($"/api/v1/catalogo/eventos/{evento.Id}", ct), 404, ct);
        Assert.Empty((await publicClient.GetFromJsonAsync<PaginaDto<EventoResumenPublicoDto>>(url, ct))!.Items);
        await using var db = fixture.CreateContext();
        Assert.True(await db.Eventos.AnyAsync(x => x.Id == evento.Id, ct));
        var schema = await publicClient.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json", ct);
        var paths = schema.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/v1/catalogo/eventos", out _));
        Assert.True(paths.TryGetProperty("/api/v1/catalogo/eventos/{id}", out _));
        Assert.True(schema.GetProperty("components").GetProperty("schemas").GetProperty("CrearTipoEntradaRequest").TryGetProperty("example", out _));
        Assert.True(schema.GetProperty("components").GetProperty("schemas").GetProperty("AsociarPresentadorRequest").TryGetProperty("example", out _));
        string[] methods = ["get", "post", "put", "delete", "patch"];
        TestContext.Current.TestOutputHelper!.WriteLine($"Operaciones OpenAPI: {paths.EnumerateObject().Sum(p => p.Value.EnumerateObject().Count(m => methods.Contains(m.Name)))}");
    }

    [Fact]
    public async Task CatalogoValidaFiltrosYOcultaEventosCanceladosODeRecintosInactivos()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var owner = factory.CreateClient(); using var publicClient = factory.CreateClient();
        await LoginAsync(owner, await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct), ct);
        int recinto = await CrearRecintoAsync(owner, ct);
        var evento = await PostAsync<EventoDto>(owner, "/api/v1/eventos", DatosEvento(recinto), ct);
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync($"/api/v1/eventos/{evento.Id}/estado", new EstadoEventoRequest("BORRADOR", "PROGRAMADO"), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await publicClient.GetAsync($"/api/v1/catalogo/eventos/{evento.Id}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.DeleteAsync($"/api/v1/recintos/{recinto}", ct)).StatusCode);
        await ProblemAsync(await publicClient.GetAsync($"/api/v1/catalogo/eventos/{evento.Id}", ct), 404, ct);
        Assert.Equal(HttpStatusCode.OK, (await owner.DeleteAsync($"/api/v1/eventos/{evento.Id}", ct)).StatusCode);
        await ProblemAsync(await publicClient.GetAsync($"/api/v1/catalogo/eventos/{evento.Id}", ct), 404, ct);
        foreach (string query in new[] { "page=0", "pageSize=101", "categoriaId=0", "desde=no-es-fecha", "desde=2028-01-01T00:00:00Z&hasta=2027-01-01T00:00:00Z", "texto=" + new string('a', 201) })
            await ProblemAsync(await publicClient.GetAsync("/api/v1/catalogo/eventos?" + query, ct), 400, ct);
        Assert.Empty((await publicClient.GetFromJsonAsync<PaginaDto<EventoResumenPublicoDto>>("/api/v1/catalogo/eventos?texto=%25", ct))!.Items);
        await ProblemAsync(await publicClient.GetAsync("/api/v1/catalogo/eventos/2147483647", ct), 404, ct);
    }
}
