using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Identity.Sessions;
using Promotora.Application.Ticketing;
using Promotora.Application.Venues;
using Promotora.Domain.Identity;
using Promotora.Domain.Shared;
using Promotora.Infrastructure.Tests;
using Promotora.Infrastructure.Ticketing;
using Promotora.Infrastructure.Ticketing.Persistence;

namespace Promotora.Api.Tests;

public sealed class BoletoValidacionApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task ValidadorCambiaPasswordYValidaSinPermisosDeGestion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var admin = factory.CreateClient();
        using var validator = factory.CreateClient();
        var support = new TicketingApiTestSupport(fixture);
        await support.LoginAsync(admin, RolUsuario.ADMINISTRADOR, ct);
        var email = $"{Guid.NewGuid():N}@example.test";
        var password = Guid.NewGuid().ToString("N");
        var alta = new { email, password, nombre = "Control", apellido = "Prueba" };
        using var create = await admin.PostAsJsonAsync("/api/v1/usuarios/validadores-boletos", alta, ct);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        async Task<TokenDto> LoginAsync(string currentPassword)
        {
            using var login = await validator.PostAsJsonAsync("/api/v1/auth/login", new { email, password = currentPassword }, ct);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var token = (await login.Content.ReadFromJsonAsync<TokenDto>(ct))!;
            validator.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            return token;
        }

        var temporary = await LoginAsync(password);
        Assert.True(temporary.DebeCambiarPassword);
        var datos = await PrepararAsync(factory, "VALIDO", ct);
        Assert.Equal(HttpStatusCode.Forbidden, (await ValidarAsync(validator, datos.Boleto.CodigoQr, ct)).StatusCode);
        var passwordNuevo = Guid.NewGuid().ToString("N");
        Assert.Equal(HttpStatusCode.OK, (await validator.PutAsJsonAsync($"/api/v1/usuarios/{temporary.UsuarioId}/password", new { passwordActual = password, passwordNuevo }, ct)).StatusCode);
        var active = await LoginAsync(passwordNuevo);
        Assert.False(active.DebeCambiarPassword);
        foreach (var route in new[] { "/api/v1/eventos", "/api/v1/recintos", "/api/v1/cuentas", "/api/v1/boletos", "/api/v1/compras", "/api/v1/usuarios", $"/api/v1/usuarios/{active.UsuarioId}" })
            Assert.Equal(HttpStatusCode.Forbidden, (await validator.GetAsync(route, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await validator.PostAsJsonAsync("/api/v1/usuarios/validadores-boletos", alta, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ValidarAsync(validator, datos.Boleto.CodigoQr, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ValidarAsync(validator, datos.Boleto.CodigoQr, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await validator.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = active.RefreshToken }, ct)).StatusCode);
    }

    [Fact]
    public async Task AdminValidaUnaVezAuditaSinQrYRechazaFirmaAlterada()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString); using var admin = factory.CreateClient();
        int usuario = await new TicketingApiTestSupport(fixture).LoginAsync(admin, RolUsuario.ADMINISTRADOR, ct);
        var datos = await PrepararAsync(factory, "VALIDO", ct);
        string alterado = datos.Boleto.CodigoQr[..^1] + (datos.Boleto.CodigoQr[^1] == 'A' ? "B" : "A");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await ValidarAsync(admin, alterado, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ValidarAsync(admin, "invalido", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ValidarAsync(admin, new HmacTicketTokenService(factory.QrKey).Firmar(Guid.NewGuid()).Value, ct)).StatusCode);
        using var response = await ValidarAsync(admin, datos.Boleto.CodigoQr, ct); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.False(body.TryGetProperty("codigoQr", out _)); Assert.False(body.TryGetProperty("asistente", out _));
        var usado = body.Deserialize<BoletoValidadoDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(datos.Boleto.PublicId, usado.PublicId); Assert.Equal("USADO", usado.Estado); Assert.Equal(DateTimeKind.Utc, usado.FechaUso.Kind);
        Assert.Equal(HttpStatusCode.Conflict, (await ValidarAsync(admin, datos.Boleto.CodigoQr, ct)).StatusCode);
        await using var db = fixture.CreateContext(); var row = await db.Boletos.AsNoTracking().SingleAsync(b => b.Id == datos.Boleto.Id, ct);
        Assert.Equal(usuario, row.ValidadoPor); Assert.Equal(usado.FechaUso, row.FechaUso);
        var audit = Assert.Single(await db.LogsAuditoria.Where(a => a.EntidadAfectada == "boletos" && a.EntidadId == row.Id.ToString() && a.Accion == "VALIDATE").ToArrayAsync(ct));
        Assert.Equal(usuario, audit.UsuarioId); Assert.Null(audit.Detalle);
        Assert.Equal(4, await db.TiposEntrada.Where(t => t.Id == datos.TipoId).Select(t => t.CupoDisponible).SingleAsync(ct));
    }

    [Fact]
    public async Task VeinteEscaneosConDosAdministradoresSoloPermitenUnaEntrada()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var uno = factory.CreateClient(); using var dos = factory.CreateClient(); var support = new TicketingApiTestSupport(fixture);
        int usuarioUno = await support.LoginAsync(uno, RolUsuario.ADMINISTRADOR, ct), usuarioDos = await support.LoginAsync(dos, RolUsuario.ADMINISTRADOR, ct);
        var datos = await PrepararAsync(factory, "VALIDO", ct);
        var respuestas = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => ValidarAsync(i % 2 == 0 ? uno : dos, datos.Boleto.CodigoQr, ct)));
        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.OK)); Assert.Equal(19, respuestas.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        await using var db = fixture.CreateContext(); var boleto = await db.Boletos.AsNoTracking().SingleAsync(b => b.Id == datos.Boleto.Id, ct);
        Assert.Equal("USADO", boleto.Estado); Assert.Contains(boleto.ValidadoPor, new int?[] { usuarioUno, usuarioDos }); Assert.NotNull(boleto.FechaUso);
        Assert.Equal(1, await db.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "boletos" && a.EntidadId == boleto.Id.ToString() && a.Accion == "VALIDATE", ct));
        foreach (var response in respuestas) response.Dispose();
    }

    [Fact]
    public async Task DuenoSoloValidaBoletosDeSuRecinto()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString); using var dueno = factory.CreateClient();
        int usuario = await new TicketingApiTestSupport(fixture).LoginAsync(dueno, RolUsuario.DUENO_RECINTO, ct);
        var datos = await PrepararAsync(factory, "VALIDO", ct);
        Assert.Equal(HttpStatusCode.NotFound, (await ValidarAsync(dueno, datos.Boleto.CodigoQr, ct)).StatusCode);
        await using var db = fixture.CreateContext(); int perfil = await db.DuenosRecinto.Where(d => d.UsuarioId == usuario).Select(d => d.Id).SingleAsync(ct);
        int recinto = await db.Eventos.Where(e => e.Id == datos.Compra.EventoId).Select(e => e.RecintoId).SingleAsync(ct);
        // Asignación de propietario de la fixture, antes de la solicitud.
        await db.Recintos.Where(r => r.Id == recinto).ExecuteUpdateAsync(s => s.SetProperty(r => r.DuenoId, perfil), ct);
        Assert.Equal(HttpStatusCode.OK, (await ValidarAsync(dueno, datos.Boleto.CodigoQr, ct)).StatusCode);
        Assert.Equal(usuario, await db.Boletos.Where(b => b.Id == datos.Boleto.Id).Select(b => b.ValidadoPor).SingleAsync(ct));
    }

    [Theory]
    [InlineData("USADO")]
    [InlineData("ANULADO")]
    public async Task EstadoIncompatibleNoSeReactiva(string estado)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString); using var admin = factory.CreateClient();
        await new TicketingApiTestSupport(fixture).LoginAsync(admin, RolUsuario.ADMINISTRADOR, ct);
        var datos = await PrepararAsync(factory, estado, ct);
        Assert.Equal(HttpStatusCode.Conflict, (await ValidarAsync(admin, datos.Boleto.CodigoQr, ct)).StatusCode);
        await using var db = fixture.CreateContext(); Assert.Equal(estado, await db.Boletos.Where(b => b.Id == datos.Boleto.Id).Select(b => b.Estado).SingleAsync(ct));
        Assert.False(await db.LogsAuditoria.AnyAsync(a => a.EntidadAfectada == "boletos" && a.EntidadId == datos.Boleto.Id.ToString() && a.Accion == "VALIDATE", ct));
    }

    [Fact]
    public async Task CompraNoCompletadaYClaveAusenteNoPermitenEntrada()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString); using var admin = factory.CreateClient();
        int usuario = await new TicketingApiTestSupport(fixture).LoginAsync(admin, RolUsuario.ADMINISTRADOR, ct);
        var datos = await PrepararAsync(factory, "VALIDO", ct); await using var db = fixture.CreateContext();
        await db.Compras.Where(c => c.PublicId == datos.Compra.PublicId && c.Estado == "COMPLETADA").ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, "PENDIENTE"), ct);
        Assert.Equal(HttpStatusCode.Conflict, (await ValidarAsync(admin, datos.Boleto.CodigoQr, ct)).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope(); var actual = new Actual(usuario);
        var access = ActivatorUtilities.CreateInstance<VenuesAccess>(scope.ServiceProvider, actual);
        var handler = ActivatorUtilities.CreateInstance<ValidarBoletoHandler>(scope.ServiceProvider, access, actual, new HmacTicketTokenService(null));
        Assert.Equal("boleto.firma", Assert.Single((await handler.HandleAsync(new(datos.Boleto.CodigoQr), ct)).Errors).Code);
        Assert.Null(await db.Boletos.Where(b => b.Id == datos.Boleto.Id).Select(b => b.FechaUso).SingleAsync(ct));
    }

    [Fact]
    public async Task AuditoriaFallidaRevierteEntradaYPermiteReintentar()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString); using var admin = factory.CreateClient();
        int usuario = await new TicketingApiTestSupport(fixture).LoginAsync(admin, RolUsuario.ADMINISTRADOR, ct);
        var datos = await PrepararAsync(factory, "VALIDO", ct);
        await using var scope = factory.Services.CreateAsyncScope(); var actual = new Actual(usuario);
        var access = ActivatorUtilities.CreateInstance<VenuesAccess>(scope.ServiceProvider, actual);
        var handler = ActivatorUtilities.CreateInstance<ValidarBoletoHandler>(scope.ServiceProvider, access, actual, new AuditFallando());
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(new(datos.Boleto.CodigoQr), ct));
        await using var db = fixture.CreateContext(); var boleto = await db.Boletos.AsNoTracking().SingleAsync(b => b.Id == datos.Boleto.Id, ct);
        Assert.Equal("VALIDO", boleto.Estado); Assert.Null(boleto.FechaUso); Assert.Null(boleto.ValidadoPor);
        Assert.Equal(HttpStatusCode.OK, (await ValidarAsync(admin, datos.Boleto.CodigoQr, ct)).StatusCode);
    }

    [Fact]
    public async Task ClienteYActorInactivoSeRechazanTambienSinController()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await ValidarAsync(client, "invalido", ct)).StatusCode);
        int usuario = await new TicketingApiTestSupport(fixture).LoginAsync(client, RolUsuario.CLIENTE, ct);
        Assert.Equal(HttpStatusCode.Forbidden, (await ValidarAsync(client, "invalido", ct)).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope(); var actual = new Actual(usuario);
        var access = ActivatorUtilities.CreateInstance<VenuesAccess>(scope.ServiceProvider, actual);
        var handler = ActivatorUtilities.CreateInstance<ValidarBoletoHandler>(scope.ServiceProvider, access, actual);
        Assert.Equal(ErrorType.Forbidden, Assert.Single((await handler.HandleAsync(new("invalido"), ct)).Errors).Type);
        usuario = await new TicketingApiTestSupport(fixture).LoginAsync(client, RolUsuario.ADMINISTRADOR, ct);
        await using var db = fixture.CreateContext(); await db.Usuarios.Where(u => u.Id == usuario && u.Estado == "ACTIVO").ExecuteUpdateAsync(s => s.SetProperty(u => u.Estado, "INACTIVO"), ct);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ValidarAsync(client, "invalido", ct)).StatusCode);
        actual = new Actual(usuario); access = ActivatorUtilities.CreateInstance<VenuesAccess>(scope.ServiceProvider, actual);
        handler = ActivatorUtilities.CreateInstance<ValidarBoletoHandler>(scope.ServiceProvider, access, actual);
        Assert.Equal(ErrorType.Forbidden, Assert.Single((await handler.HandleAsync(new("invalido"), ct)).Errors).Type);
    }

    [Fact]
    public async Task SwaggerDocumentaOperacionYErroresSinEjemplosDeTokensReales()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString); using var client = factory.CreateClient();
        var swagger = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json", ct);
        var operacion = swagger.GetProperty("paths").GetProperty("/api/v1/boletos/validar").GetProperty("post");
        foreach (var codigo in new[] { "200", "400", "401", "403", "404", "409", "422" }) Assert.True(operacion.GetProperty("responses").TryGetProperty(codigo, out _));
        Assert.True(operacion.TryGetProperty("requestBody", out _));
    }

    private async Task<(CompraCreadaDto Compra, BoletoRow Boleto, int TipoId)> PrepararAsync(IdentityApiFactory factory, string estado, CancellationToken ct)
    {
        await using var db = fixture.CreateContext(); var datos = await CompraFixture.CrearAsync(db, 5, ct);
        await db.TiposEntrada.Where(t => t.Id == datos.TipoId).ExecuteUpdateAsync(s => s.SetProperty(t => t.Precio, 0m), ct);
        using var cliente = factory.CreateClient(); await new TicketingApiTestSupport(fixture).LoginAsync(cliente, RolUsuario.CLIENTE, ct);
        var compra = await TicketingApiTestSupport.ComprarAsync(cliente, datos.EventoId, datos.TipoId, 1, ct);
        return (compra, await TicketingApiTestSupport.BoletoGratisAsync(db, compra.PublicId, factory.QrKey, estado, ct), datos.TipoId);
    }
    private static Task<HttpResponseMessage> ValidarAsync(HttpClient client, string qr, CancellationToken ct) => client.PostAsJsonAsync("/api/v1/boletos/validar", new { codigoQr = qr }, ct);
    private sealed record Actual(int? UsuarioId) : IUsuarioActual { public string? IpOrigen => null; }
    private sealed class AuditFallando : IAuditLogger
    {
        public Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, CancellationToken ct) => throw new InvalidOperationException("Fallo de auditoría de prueba.");
        public Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, DetalleAuditoria? detalle, CancellationToken ct) => throw new InvalidOperationException("Fallo de auditoría de prueba.");
    }
}
