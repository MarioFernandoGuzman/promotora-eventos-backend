using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Identity.Registration;
using Promotora.Application.Identity.Sessions;
using Promotora.Application.Venues;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Audit;
using Promotora.Infrastructure.Identity;
using Promotora.Infrastructure.Identity.Persistence;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Tests;

namespace Promotora.Api.Tests;

public sealed partial class VenuesApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task DuenoGestionaRecintosPropiosAdministradorTodosYBajaEsIdempotente()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct);
        var other = await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct);
        var admin = await CreateUserAsync(RolUsuario.ADMINISTRADOR, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await LoginAsync(client, owner, ct);
        var body = new { nombre = "Recinto", capacidadTotal = 100 };
        var created = await client.PostAsJsonAsync("/api/v1/recintos", body, ct);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var recinto = (await created.Content.ReadFromJsonAsync<RecintoDto>(ct))!;
        await using var db = fixture.CreateContext();
        Assert.Equal((await db.DuenosRecinto.SingleAsync(x => x.UsuarioId == owner.Id, ct)).Id, recinto.DuenoId);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/recintos/{recinto.Id}", ct)).StatusCode);
        var page = (await client.GetFromJsonAsync<RecintosPaginaDto>("/api/v1/recintos?page=1&pageSize=1", ct))!;
        Assert.Single(page.Items);
        Assert.Equal(1, page.Total);
        await LoginAsync(client, other, ct);
        await ProblemAsync(await client.GetAsync($"/api/v1/recintos/{recinto.Id}", ct), 403, ct);
        await ProblemAsync(await client.PutAsJsonAsync($"/api/v1/recintos/{recinto.Id}", body, ct), 403, ct);
        await ProblemAsync(await client.DeleteAsync($"/api/v1/recintos/{recinto.Id}", ct), 403, ct);
        await ProblemAsync(await client.PostAsJsonAsync("/api/v1/recintos", new { nombre = "Ajeno", duenoId = recinto.DuenoId }, ct), 403, ct);
        Assert.Empty((await client.GetFromJsonAsync<RecintosPaginaDto>("/api/v1/recintos", ct))!.Items);
        await LoginAsync(client, admin, ct);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/v1/recintos/{recinto.Id}", new { nombre = "Editado", capacidadTotal = 150 }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/recintos", new { nombre = "Admin", duenoId = recinto.DuenoId }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/v1/recintos/{recinto.Id}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/v1/recintos/{recinto.Id}", ct)).StatusCode);
        Assert.False((await db.Recintos.AsNoTracking().SingleAsync(x => x.Id == recinto.Id, ct)).Activo);
        Assert.Equal(1, await db.LogsAuditoria.CountAsync(x => x.EntidadAfectada == "recintos" && x.EntidadId == recinto.Id.ToString() && x.Accion == "DELETE", ct));
        await ProblemAsync(await client.PutAsJsonAsync($"/api/v1/recintos/{recinto.Id}", body, ct), 409, ct);
        await ProblemAsync(await client.GetAsync("/api/v1/recintos/2147483647", ct), 404, ct);
    }

    [Fact]
    public async Task RecintosRechazanAnonimosClientesYCamposInvalidosYSwaggerDocumentaBearer()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await ProblemAsync(await client.GetAsync("/api/v1/recintos", ct), 401, ct);
        await LoginAsync(client, await CreateUserAsync(RolUsuario.CLIENTE, ct), ct);
        await ProblemAsync(await client.PostAsJsonAsync("/api/v1/recintos", new { nombre = "Recinto" }, ct), 403, ct);
        await LoginAsync(client, await CreateUserAsync(RolUsuario.DUENO_RECINTO, ct), ct);
        await ProblemAsync(await client.PostAsJsonAsync("/api/v1/recintos", new { nombre = " ", capacidadTotal = -1, latitud = 91m }, ct), 400, ct);
        await ProblemAsync(await client.GetAsync("/api/v1/recintos?pageSize=101", ct), 400, ct);
        var schema = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json", ct);
        Assert.True(schema.GetProperty("paths").GetProperty("/api/v1/recintos").GetProperty("post").TryGetProperty("security", out _));
        Assert.True(schema.GetProperty("components").GetProperty("schemas").GetProperty("CrearRecintoRequest").TryGetProperty("example", out _));
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
        var token = (await response.Content.ReadFromJsonAsync<TokenDto>(ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
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
