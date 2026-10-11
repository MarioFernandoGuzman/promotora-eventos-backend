using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Catalog;
using Promotora.Application.Identity.Registration;
using Promotora.Application.Identity.Sessions;
using Promotora.Application.Shared;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Audit;
using Promotora.Infrastructure.Identity;
using Promotora.Infrastructure.Identity.Persistence;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Tests;

namespace Promotora.Api.Tests;

public sealed partial class CatalogApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task AdministradorGestionaCategoriasYPresentadoresConBajaLogicaYAuditoria()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await CreateUserAsync(RolUsuario.ADMINISTRADOR, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await ProblemAsync(await client.GetAsync("/api/v1/categorias", ct), 401, ct);
        await LoginAsync(client, admin, ct);
        string nombre = Guid.NewGuid().ToString("N");
        var category = await PostAsync<CategoriaEventoDto>(client, "/api/v1/categorias", new { nombre, descripcion = "Prueba" }, ct);
        await ProblemAsync(await client.PostAsJsonAsync("/api/v1/categorias", new { nombre }, ct), 409, ct);
        Assert.True((await client.GetFromJsonAsync<PaginaDto<CategoriaEventoDto>>("/api/v1/categorias?page=1&pageSize=1", ct))!.Total >= 7);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/v1/categorias/{category.Id}", new { nombre, descripcion = "Editada" }, ct)).StatusCode);
        var presentador = await PostAsync<PresentadorDto>(client, "/api/v1/presentadores", new { nombre = "Artista", tipo = "ARTISTA" }, ct);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/v1/presentadores/{presentador.Id}", new { nombre = "Equipo", tipo = "EQUIPO", biografia = "Texto" }, ct)).StatusCode);
        await ProblemAsync(await client.PostAsJsonAsync("/api/v1/presentadores", new { nombre = "No válido", tipo = "DJ" }, ct), 400, ct);
        await ProblemAsync(await client.PostAsJsonAsync("/api/v1/categorias", new { nombre = " " }, ct), 400, ct);
        await ProblemAsync(await client.GetAsync("/api/v1/categorias?pageSize=101", ct), 400, ct);
        foreach (string url in new[] { $"/api/v1/categorias/{category.Id}", $"/api/v1/presentadores/{presentador.Id}" })
        {
            Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync(url, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync(url, ct)).StatusCode);
        }

        Assert.False((await client.GetFromJsonAsync<CategoriaEventoDto>($"/api/v1/categorias/{category.Id}", ct))!.Activo);
        Assert.False((await client.GetFromJsonAsync<PresentadorDto>($"/api/v1/presentadores/{presentador.Id}", ct))!.Activo);
        await ProblemAsync(await client.PutAsJsonAsync($"/api/v1/categorias/{category.Id}", new { nombre }, ct), 409, ct);
        await ProblemAsync(await client.GetAsync("/api/v1/presentadores/2147483647", ct), 404, ct);
        await using var db = fixture.CreateContext();
        Assert.Equal(1, await db.LogsAuditoria.CountAsync(x => x.EntidadAfectada == "categorias_evento" && x.EntidadId == category.Id.ToString() && x.Accion == "DELETE", ct));
        Assert.Equal(1, await db.LogsAuditoria.CountAsync(x => x.EntidadAfectada == "categorias_evento" && x.EntidadId == category.Id.ToString() && x.Accion == "CREATE", ct));
        Assert.True(await db.Presentadores.AnyAsync(x => x.Id == presentador.Id && !x.Activo, ct));
        var schema = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json", ct);
        Assert.True(schema.GetProperty("components").GetProperty("schemas").GetProperty("PresentadorRequest").TryGetProperty("example", out _));
    }

    [Theory]
    [InlineData(RolUsuario.CLIENTE)]
    [InlineData(RolUsuario.DUENO_RECINTO)]
    public async Task ClienteYDuenoConsultanPeroNoModificanCatalogos(RolUsuario rol)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await LoginAsync(client, await CreateUserAsync(rol, ct), ct);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/categorias", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/presentadores", ct)).StatusCode);
        await ProblemAsync(await client.PostAsJsonAsync("/api/v1/categorias", new { nombre = "No permitido" }, ct), 403, ct);
        await ProblemAsync(await client.PostAsJsonAsync("/api/v1/presentadores", new { nombre = "No permitido", tipo = "ARTISTA" }, ct), 403, ct);
        await ProblemAsync(await client.DeleteAsync("/api/v1/categorias/1", ct), 403, ct);
        await ProblemAsync(await client.PutAsJsonAsync("/api/v1/presentadores/1", new { nombre = "Ajeno", tipo = "EQUIPO" }, ct), 403, ct);
    }

    private async Task<(int Id, string Email, string Password)> CreateUserAsync(RolUsuario rol, CancellationToken ct)
    {
        await using var db = fixture.CreateContext();
        var clock = new SystemClock(TimeProvider.System);
        var password = Guid.NewGuid().ToString("N");
        string email = $"{Guid.NewGuid():N}@example.test";
        var repository = new EfUsuarioRepository(db, new EfUnitOfWork(db), new EfAuditLogger(db, clock), new Promotora.Infrastructure.Finance.EfCuentaRepository(db, new EfAuditLogger(db, clock)));
        var result = await repository.CreateAsync(new Usuario(0, email, new IdentityPasswordService().Hash(password), "Nombre", "Apellido",
            rol, "ACTIVO", false, clock.UtcNow), new PerfilNuevo(Empresa: rol == RolUsuario.DUENO_RECINTO ? "Empresa" : null), null, null, false, ct);
        return (result.Value.Id, email, password);
    }

    private static async Task LoginAsync(HttpClient client, (int Id, string Email, string Password) user, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { user.Email, user.Password }, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<TokenDto>(ct))!.AccessToken);
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync(url, body, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(ct))!;
    }

    private static async Task ProblemAsync(HttpResponseMessage response, int status, CancellationToken ct)
    {
        using (response)
        {
            Assert.Equal(status, (int)response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            Assert.Equal(status, json.GetProperty("status").GetInt32());
            Assert.True(json.TryGetProperty("traceId", out _));
        }
    }
}
