using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Promotora.Application.Identity.Registration;
using Promotora.Application.Identity.Sessions;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Audit;
using Promotora.Infrastructure.Identity;
using Promotora.Infrastructure.Identity.Persistence;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Tests;

namespace Promotora.Api.Tests;

public sealed class IdentityApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task BootstrapLoginLimitadoCambioPasswordYAltasFuncionanConJwtReal()
    {
        var ct = TestContext.Current.CancellationToken;
        var email = $"{Guid.NewGuid():N}@example.test";
        var password = Guid.NewGuid().ToString("N");
        await using var factory = new IdentityApiFactory(fixture.ConnectionString, email, password);
        using var client = factory.CreateClient();
        var initial = await LoginAsync(client, email, password, ct);
        Assert.True(initial.DebeCambiarPassword);
        Assert.Null(initial.RefreshToken);
        Bearer(client, initial.AccessToken);
        await ProblemAsync(await client.GetAsync("/api/v1/usuarios", ct), 403, ct);
        string next = Guid.NewGuid().ToString("N");
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/v1/usuarios/{initial.UsuarioId}/password", new { passwordActual = password, passwordNuevo = next }, ct)).StatusCode);
        await ProblemAsync(await client.GetAsync("/api/v1/usuarios", ct), 401, ct);
        var full = await LoginAsync(client, email, next, ct);
        Assert.False(full.DebeCambiarPassword);
        Bearer(client, full.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/usuarios?page=1&pageSize=1", ct)).StatusCode);
        var admin = await client.PostAsJsonAsync("/api/v1/usuarios/administradores", new
        {
            email = $"{Guid.NewGuid():N}@example.test",
            password = Guid.NewGuid().ToString("N"),
            nombre = "Admin",
            apellido = "Prueba"
        }, ct);
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
        var adminDto = await admin.Content.ReadFromJsonAsync<UsuarioDto>(ct);
        var owner = await client.PostAsJsonAsync("/api/v1/usuarios/duenos-recinto", new
        {
            email = $"{Guid.NewGuid():N}@example.test",
            password = Guid.NewGuid().ToString("N"),
            nombre = "Dueño",
            apellido = "Prueba",
            empresa = "Empresa"
        }, ct);
        Assert.Equal(HttpStatusCode.OK, owner.StatusCode);
        await using var db = fixture.CreateContext();
        Assert.Equal("ESTANDAR", (await db.Administradores.SingleAsync(x => x.UsuarioId == adminDto!.Id, ct)).NivelAcceso);
        Assert.True(await db.LogsAuditoria.AnyAsync(x => x.UsuarioId == initial.UsuarioId && x.Accion == "LOGIN", ct));
        Assert.True(await db.LogsAuditoria.AnyAsync(x => x.UsuarioId == initial.UsuarioId && x.Accion == "UPDATE", ct));
    }

    [Theory]
    [InlineData(RolUsuario.CLIENTE)]
    [InlineData(RolUsuario.DUENO_RECINTO)]
    public async Task ClienteYDuenoSoloAccedenASuUsuarioYNoPuedenCrearAdministradores(RolUsuario rol)
    {
        var ct = TestContext.Current.CancellationToken;
        var (user, password) = await CreateAsync(rol, ct);
        var (other, _) = await CreateAsync(RolUsuario.CLIENTE, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        Bearer(client, (await LoginAsync(client, user.Email, password, ct)).AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/usuarios/{user.Id}", ct)).StatusCode);
        await ProblemAsync(await client.GetAsync("/api/v1/usuarios", ct), 403, ct);
        await ProblemAsync(await client.GetAsync($"/api/v1/usuarios/{other.Id}", ct), 403, ct);
        await ProblemAsync(await client.DeleteAsync($"/api/v1/usuarios/{other.Id}", ct), 403, ct);
        await ProblemAsync(await client.PutAsJsonAsync($"/api/v1/usuarios/{other.Id}", new
        {
            email = other.Email,
            nombre = "Fraude",
            apellido = "Prueba"
        }, ct), 403, ct);
        await ProblemAsync(await client.PutAsJsonAsync($"/api/v1/usuarios/{other.Id}/password", new
        {
            passwordActual = password,
            passwordNuevo = Guid.NewGuid().ToString("N")
        }, ct), 403, ct);
        await ProblemAsync(await client.PostAsJsonAsync("/api/v1/usuarios/administradores", new
        {
            email = $"{Guid.NewGuid():N}@example.test",
            password = Guid.NewGuid().ToString("N"),
            nombre = "Admin",
            apellido = "Prueba"
        }, ct), 403, ct);
        await ProblemAsync(await client.PostAsJsonAsync("/api/v1/usuarios/duenos-recinto", new
        {
            email = $"{Guid.NewGuid():N}@example.test",
            password = Guid.NewGuid().ToString("N"),
            nombre = "Dueño",
            apellido = "Prueba",
            empresa = "Empresa"
        }, ct), 403, ct);
    }

    [Fact]
    public async Task RegistroAnonimoValidaDatosYNuncaDevuelveHash()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.test";
        var password = Guid.NewGuid().ToString("N");
        var body = new { email = email.ToUpperInvariant(), password, nombre = "Cliente", apellido = "Prueba" };
        var response = await client.PostAsJsonAsync("/api/v1/auth/registro", body, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(email, json.GetProperty("email").GetString());
        Assert.False(json.TryGetProperty("passwordHash", out _));
        Assert.False(json.TryGetProperty("password", out _));
        await ProblemAsync(await client.PostAsJsonAsync("/api/v1/auth/registro", body, ct), 409, ct);
        await ProblemAsync(await client.PostAsJsonAsync("/api/v1/auth/registro", new { email = "incorrecto", password = "corta", nombre = "", apellido = "" }, ct), 400, ct);
        await ProblemAsync(await client.GetAsync("/api/v1/usuarios", ct), 401, ct);
    }

    [Theory]
    [InlineData("firma")]
    [InlineData("emisor")]
    [InlineData("audiencia")]
    [InlineData("expiracion")]
    [InlineData("algoritmo")]
    public async Task JwtManipuladoSeRechaza(string corruption)
    {
        var ct = TestContext.Current.CancellationToken;
        var (user, password) = await CreateAsync(RolUsuario.CLIENTE, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        var valid = await LoginAsync(client, user.Email, password, ct);
        var decoded = new JwtSecurityTokenHandler().ReadJwtToken(valid.AccessToken);
        var key = corruption == "firma" ? System.Security.Cryptography.RandomNumberGenerator.GetBytes(32) : factory.Key;
        var claims = decoded.Claims.Where(x => x.Type is not ("exp" or "nbf" or "iat" or "iss" or "aud"));
        var token = new JwtSecurityToken(corruption == "emisor" ? "otro" : "promotora", corruption == "audiencia" ? "otra" : "promotora-api",
            claims, DateTime.UnixEpoch, corruption == "expiracion" ? DateTime.UnixEpoch.AddMinutes(1) : valid.ExpiraEn,
            new SigningCredentials(new SymmetricSecurityKey(key), corruption == "algoritmo" ? SecurityAlgorithms.HmacSha384 : SecurityAlgorithms.HmacSha256));
        Bearer(client, new JwtSecurityTokenHandler().WriteToken(token));
        await ProblemAsync(await client.GetAsync($"/api/v1/usuarios/{user.Id}", ct), 401, ct);
    }

    [Fact]
    public async Task RefreshReutilizadoYLogoutInvalidanJwtInmediatamente()
    {
        var ct = TestContext.Current.CancellationToken;
        var (user, password) = await CreateAsync(RolUsuario.CLIENTE, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        var login = await LoginAsync(client, user.Email, password, ct);
        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = login.RefreshToken }, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var next = (await response.Content.ReadFromJsonAsync<TokenDto>(ct))!;
        Bearer(client, login.AccessToken);
        await ProblemAsync(await client.GetAsync($"/api/v1/usuarios/{user.Id}", ct), 401, ct);
        Bearer(client, next.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/usuarios/{user.Id}", ct)).StatusCode);
        await ProblemAsync(await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = login.RefreshToken }, ct), 401, ct);
        await ProblemAsync(await client.GetAsync($"/api/v1/usuarios/{user.Id}", ct), 401, ct);
        var fresh = await LoginAsync(client, user.Email, password, ct);
        Bearer(client, fresh.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = fresh.RefreshToken }, ct)).StatusCode);
        await ProblemAsync(await client.GetAsync($"/api/v1/usuarios/{user.Id}", ct), 401, ct);
    }

    [Fact]
    public async Task BajaConservaPerfilYDeniegaLoginYTokenAnterior()
    {
        var ct = TestContext.Current.CancellationToken;
        var (user, password) = await CreateAsync(RolUsuario.CLIENTE, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        Bearer(client, (await LoginAsync(client, user.Email, password, ct)).AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/v1/usuarios/{user.Id}", ct)).StatusCode);
        await ProblemAsync(await client.GetAsync($"/api/v1/usuarios/{user.Id}", ct), 401, ct);
        await ProblemAsync(await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password }, ct), 401, ct);
        await using var db = fixture.CreateContext();
        Assert.True(await db.Clientes.AnyAsync(x => x.UsuarioId == user.Id, ct));
        Assert.Equal("INACTIVO", (await db.Usuarios.SingleAsync(x => x.Id == user.Id, ct)).Estado);
    }

    private async Task<(UsuarioDto Usuario, string Password)> CreateAsync(RolUsuario rol, CancellationToken ct)
    {
        await using var db = fixture.CreateContext();
        var clock = new SystemClock(TimeProvider.System);
        var passwords = new IdentityPasswordService();
        var password = Guid.NewGuid().ToString("N");
        var repo = new EfUsuarioRepository(db, new EfUnitOfWork(db), new EfAuditLogger(db, clock));
        var result = await repo.CreateAsync(new Usuario(0, $"{Guid.NewGuid():N}@example.test", passwords.Hash(password), "Nombre", "Apellido",
            rol, "ACTIVO", false, clock.UtcNow), new PerfilNuevo(Empresa: rol == RolUsuario.DUENO_RECINTO ? "Empresa" : null), null, null, false, ct);
        return (result.Value, password);
    }

    private static async Task<TokenDto> LoginAsync(HttpClient client, string email, string password, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TokenDto>(ct))!;
    }

    private static void Bearer(HttpClient client, string token) => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task ProblemAsync(HttpResponseMessage response, int status, CancellationToken ct)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(status, problem.GetProperty("status").GetInt32());
        Assert.True(problem.TryGetProperty("traceId", out _));
    }
}
