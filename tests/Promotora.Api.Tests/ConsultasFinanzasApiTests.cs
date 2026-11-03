using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Finance;
using Promotora.Application.Identity.Sessions;
using Promotora.Application.Shared;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Identity;
using Promotora.Infrastructure.Tests;

namespace Promotora.Api.Tests;

public sealed class ConsultasFinanzasApiTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task PermisosPropiedadListadosYCuentaCerrada()
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString);
        using var publico = factory.CreateClient(); using var cliente = factory.CreateClient(); using var dueno = factory.CreateClient(); using var admin = factory.CreateClient();
        var soporte = new TicketingApiTestSupport(postgres); await soporte.LoginAsync(cliente, RolUsuario.CLIENTE, Ct);
        int propietario = await soporte.LoginAsync(dueno, RolUsuario.DUENO_RECINTO, Ct); await soporte.LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct);
        await using var db = postgres.CreateContext(); int perfil = await db.DuenosRecinto.Where(d => d.UsuarioId == propietario).Select(d => d.Id).SingleAsync(Ct);
        int cuenta = await db.CuentasFinancieras.Where(c => c.DuenoId == perfil).Select(c => c.Id).SingleAsync(Ct);
        int sistema = await db.CuentasFinancieras.Where(c => c.Tipo == "PLATAFORMA" && c.Moneda == "GTQ").Select(c => c.Id).SingleAsync(Ct);
        foreach (string ruta in new[] { "/api/v1/cuentas", $"/api/v1/cuentas/{cuenta}", $"/api/v1/cuentas/{cuenta}/movimientos" })
        {
            using var anonima = await publico.GetAsync(ruta, Ct); Assert.Equal(HttpStatusCode.Unauthorized, anonima.StatusCode);
            using var prohibida = await cliente.GetAsync(ruta, Ct); Assert.Equal(HttpStatusCode.Forbidden, prohibida.StatusCode);
        }
        var propias = (await dueno.GetFromJsonAsync<PaginaDto<CuentaDto>>("/api/v1/cuentas", Ct))!;
        Assert.Equal(1, propias.Page); Assert.Equal(20, propias.PageSize); Assert.Equal(cuenta, Assert.Single(propias.Items).Id);
        Assert.Equal(1, propias.Total);
        using var ajena = await dueno.GetAsync($"/api/v1/cuentas/{sistema}", Ct); Assert.Equal(HttpStatusCode.NotFound, ajena.StatusCode);
        using var extractoAjeno = await dueno.GetAsync($"/api/v1/cuentas/{sistema}/movimientos", Ct); Assert.Equal(HttpStatusCode.NotFound, extractoAjeno.StatusCode);
        var filtradas = (await dueno.GetFromJsonAsync<PaginaDto<CuentaDto>>("/api/v1/cuentas?tipo=PLATAFORMA", Ct))!; Assert.Empty(filtradas.Items); Assert.Equal(0, filtradas.Total);
        var filtroAjeno = (await dueno.GetFromJsonAsync<PaginaDto<CuentaDto>>($"/api/v1/cuentas?duenoId={int.MaxValue}", Ct))!; Assert.Empty(filtroAjeno.Items);
        var todas = (await admin.GetFromJsonAsync<PaginaDto<CuentaDto>>("/api/v1/cuentas?pageSize=100", Ct))!;
        Assert.Contains(todas.Items, c => c.Id == sistema); Assert.Contains(todas.Items, c => c.Id == cuenta);
        using var cierre = await dueno.PostAsync($"/api/v1/cuentas/{cuenta}/cerrar", null, Ct); Assert.Equal(HttpStatusCode.OK, cierre.StatusCode);
        var cerrada = (await dueno.GetFromJsonAsync<CuentaDto>($"/api/v1/cuentas/{cuenta}", Ct))!; Assert.Equal("CERRADA", cerrada.Estado); Assert.NotNull(cerrada.FechaCierre);
        var cerradas = (await dueno.GetFromJsonAsync<PaginaDto<CuentaDto>>("/api/v1/cuentas?estado=CERRADA", Ct))!; Assert.Equal(cuenta, Assert.Single(cerradas.Items).Id);
        var vacio = (await dueno.GetFromJsonAsync<PaginaDto<MovimientoCuentaDto>>($"/api/v1/cuentas/{cuenta}/movimientos", Ct))!; Assert.Empty(vacio.Items); Assert.Equal(0, vacio.Total);
    }
    [Fact]
    public async Task ExtractoDeVentaYRetiroFiltraOrdenaYPaginaSinExponerReferencias()
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString); using var admin = factory.CreateClient(); using var dueno = factory.CreateClient();
        await new TicketingApiTestSupport(postgres).LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct);
        await using var db = postgres.CreateContext(); var venta = await ReembolsoFixture.VentaAsync(db, postgres.ConnectionString, factory.QrKey, false, Ct);
        int cuenta = await db.MovimientosFinancieros.Where(m => m.CompraId == venta.Id && m.Tipo == "VENTA").Select(m => m.CuentaId).SingleAsync(Ct);
        var perfil = await db.CuentasFinancieras.Where(c => c.Id == cuenta).Select(c => c.DuenoId!.Value).SingleAsync(Ct);
        // Habilita únicamente el login del dueño de esta venta temporal, sin fabricar JWT.
        int usuario = await db.DuenosRecinto.Where(d => d.Id == perfil).Select(d => d.UsuarioId).SingleAsync(Ct);
        string password = Guid.NewGuid().ToString("N"), hash = new IdentityPasswordService().Hash(password);
        await db.Usuarios.Where(u => u.Id == usuario).ExecuteUpdateAsync(s => s.SetProperty(u => u.PasswordHash, hash), Ct);
        string email = await db.Usuarios.Where(u => u.Id == usuario).Select(u => u.Email).SingleAsync(Ct);
        using var login = await dueno.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, Ct); Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        dueno.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<TokenDto>(Ct))!.AccessToken);
        using var retiro = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/cuentas/{cuenta}/liquidar") { Content = JsonContent.Create(new { monto = 100m }) };
        retiro.Headers.Add("Idempotency-Key", "consulta-retiro"); using var respuesta = await admin.SendAsync(retiro, Ct); Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        string ruta = $"/api/v1/cuentas/{cuenta}/movimientos";
        var extracto = (await dueno.GetFromJsonAsync<PaginaDto<MovimientoCuentaDto>>(ruta, Ct))!;
        Assert.Equal(2, extracto.Total); Assert.Equal(new[] { "LIQUIDACION", "VENTA" }, extracto.Items.Select(m => m.Tipo));
        Assert.Equal(-100m, extracto.Items[0].Monto); Assert.Null(extracto.Items[0].CompraId); Assert.Equal(venta.Id, extracto.Items[1].CompraId);
        Assert.All(extracto.Items, m => Assert.Equal(DateTimeKind.Utc, m.Fecha.Kind));
        var pagina = (await dueno.GetFromJsonAsync<PaginaDto<MovimientoCuentaDto>>(ruta + "?page=2&pageSize=1", Ct))!;
        Assert.Equal(2, pagina.Total); Assert.Equal(extracto.Items[1], Assert.Single(pagina.Items));
        var fuera = (await dueno.GetFromJsonAsync<PaginaDto<MovimientoCuentaDto>>(ruta + "?page=99", Ct))!; Assert.Empty(fuera.Items); Assert.Equal(2, fuera.Total);
        var compra = (await dueno.GetFromJsonAsync<PaginaDto<MovimientoCuentaDto>>(ruta + $"?tipo=VENTA&compraId={venta.Id}", Ct))!; Assert.Equal(extracto.Items[1], Assert.Single(compra.Items));
        string desde = Uri.EscapeDataString(extracto.Items[1].Fecha.ToString("O")), hasta = Uri.EscapeDataString(extracto.Items[0].Fecha.ToString("O"));
        var intervalo = (await dueno.GetFromJsonAsync<PaginaDto<MovimientoCuentaDto>>(ruta + $"?desde={desde}&hasta={hasta}", Ct))!;
        Assert.Equal(extracto.Items[1], Assert.Single(intervalo.Items));
        var cuentas = (await admin.GetFromJsonAsync<PaginaDto<CuentaDto>>($"/api/v1/cuentas?duenoId={perfil}&moneda=GTQ&tipo=DUENO&estado=ACTIVA&pageSize=1", Ct))!;
        Assert.Equal(cuenta, Assert.Single(cuentas.Items).Id); Assert.Equal(200m, cuentas.Items[0].Saldo); Assert.Equal(1, cuentas.Total);
        var json = await dueno.GetFromJsonAsync<JsonElement>(ruta, Ct); Assert.False(json.GetProperty("items")[0].TryGetProperty("descripcion", out _));
    }
    [Fact]
    public async Task ContratoValidacion404YSwagger()
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString); using var admin = factory.CreateClient();
        await new TicketingApiTestSupport(postgres).LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct);
        foreach (string filtro in new[] { "page=0", "page=100001", "pageSize=101", "duenoId=0", "moneda=gtq", "tipo=CLIENTE", "estado=PENDIENTE" })
        { using var response = await admin.GetAsync("/api/v1/cuentas?" + filtro, Ct); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); }
        foreach (string filtro in new[] { "page=0", "pageSize=0", "compraId=0", "tipo=OTRO", "desde=2026-01-02T00:00:00Z&hasta=2026-01-01T00:00:00Z", "desde=2026-01-01T00:00:00Z&hasta=2026-01-01T00:00:00Z", "desde=2026-01-01T00:00:00%2B01:00" })
        { using var response = await admin.GetAsync("/api/v1/cuentas/1/movimientos?" + filtro, Ct); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); }
        foreach (string ruta in new[] { "/api/v1/cuentas/0", "/api/v1/cuentas/0/movimientos" })
        { using var response = await admin.GetAsync(ruta, Ct); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); }
        foreach (string ruta in new[] { "/api/v1/cuentas/2147483647", "/api/v1/cuentas/2147483647/movimientos" })
        { using var response = await admin.GetAsync(ruta, Ct); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType); }
        var swagger = await admin.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json", Ct);
        foreach (string ruta in new[] { "/api/v1/cuentas", "/api/v1/cuentas/{id}", "/api/v1/cuentas/{id}/movimientos" })
        {
            var operacion = swagger.GetProperty("paths").GetProperty(ruta).GetProperty("get"); Assert.True(operacion.TryGetProperty("security", out _));
            foreach (string codigo in new[] { "200", "400", "401", "403" }) Assert.True(operacion.GetProperty("responses").TryGetProperty(codigo, out _));
        }
    }
    [Theory]
    [InlineData("INACTIVO")]
    [InlineData("PASSWORD")]
    public async Task SesionPreviaAInhabilitacionNoConsultaFinanzas(string cambio)
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString); using var actor = factory.CreateClient();
        int usuario = await new TicketingApiTestSupport(postgres).LoginAsync(actor, RolUsuario.DUENO_RECINTO, Ct);
        await using var db = postgres.CreateContext();
        if (cambio == "INACTIVO") await db.Usuarios.Where(u => u.Id == usuario && u.Estado == "ACTIVO").ExecuteUpdateAsync(s => s.SetProperty(u => u.Estado, "INACTIVO"), Ct);
        else await db.Usuarios.Where(u => u.Id == usuario).ExecuteUpdateAsync(s => s.SetProperty(u => u.DebeCambiarPassword, true), Ct);
        foreach (string ruta in new[] { "/api/v1/cuentas", "/api/v1/cuentas/1", "/api/v1/cuentas/1/movimientos" })
        { using var response = await actor.GetAsync(ruta, Ct); Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden }); }
    }
}
