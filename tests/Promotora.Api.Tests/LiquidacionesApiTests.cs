using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Finance;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Tests;

namespace Promotora.Api.Tests;

public sealed class LiquidacionesApiTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task ContratoAutorizacionIdempotenciaValidacionYSwagger()
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString);
        using var publico = factory.CreateClient(); using var cliente = factory.CreateClient(); using var dueno = factory.CreateClient(); using var admin = factory.CreateClient();
        var soporte = new TicketingApiTestSupport(postgres);
        await soporte.LoginAsync(cliente, RolUsuario.CLIENTE, Ct); await soporte.LoginAsync(dueno, RolUsuario.DUENO_RECINTO, Ct); await soporte.LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct);
        await using var db = postgres.CreateContext(); var venta = await ReembolsoFixture.VentaAsync(db, postgres.ConnectionString, factory.QrKey, false, Ct);
        int cuenta = await db.MovimientosFinancieros.Where(m => m.CompraId == venta.Id && m.Tipo == "VENTA").Select(m => m.CuentaId).SingleAsync(Ct);
        using var anonima = await EnviarAsync(publico, cuenta, 100m, "clave"); Assert.Equal(HttpStatusCode.Unauthorized, anonima.StatusCode);
        foreach (var actor in new[] { cliente, dueno }) { using var prohibida = await EnviarAsync(actor, cuenta, 100m, "clave"); Assert.Equal(HttpStatusCode.Forbidden, prohibida.StatusCode); }
        using var primero = await EnviarAsync(admin, cuenta, 100m, "clave"); Assert.Equal(HttpStatusCode.OK, primero.StatusCode);
        var payload = (await primero.Content.ReadFromJsonAsync<LiquidacionDto>(Ct))!;
        Assert.Equal(cuenta, payload.CuentaId); Assert.Equal(100m, payload.Monto); Assert.Equal("GTQ", payload.Moneda);
        using var repetida = await EnviarAsync(admin, cuenta, 100m, "clave"); Assert.Equal(HttpStatusCode.OK, repetida.StatusCode);
        Assert.Equal(payload, await repetida.Content.ReadFromJsonAsync<LiquidacionDto>(Ct));
        using var cambio = await EnviarAsync(admin, cuenta, 101m, "clave"); Assert.Equal(HttpStatusCode.Conflict, cambio.StatusCode);
        foreach (var monto in new[] { 0m, -1m, .001m, 10000000000m })
        { using var invalida = await EnviarAsync(admin, cuenta, monto, "invalida"); Assert.Equal(HttpStatusCode.BadRequest, invalida.StatusCode); }
        foreach (var clave in new string?[] { null, "espacio no", "á", new('a', 129) })
        { using var invalida = await EnviarAsync(admin, cuenta, 1m, clave); Assert.Equal(HttpStatusCode.BadRequest, invalida.StatusCode); }
        using var inexistente = await EnviarAsync(admin, int.MaxValue, 1m, "clave"); Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        using var idInvalido = await EnviarAsync(admin, 0, 1m, "clave"); Assert.Equal(HttpStatusCode.BadRequest, idInvalido.StatusCode);
        using var insuficiente = await EnviarAsync(admin, cuenta, 201m, "insuficiente"); Assert.Equal(HttpStatusCode.Conflict, insuficiente.StatusCode);
        Assert.Equal("application/problem+json", insuficiente.Content.Headers.ContentType!.MediaType);
        foreach (string tipo in new[] { "PLATAFORMA", "IMPUESTOS" })
        {
            int id = await db.CuentasFinancieras.Where(c => c.Tipo == tipo && c.Moneda == "GTQ").Select(c => c.Id).SingleAsync(Ct);
            using var sistema = await EnviarAsync(admin, id, 1m, "sistema"); Assert.Equal(HttpStatusCode.Conflict, sistema.StatusCode);
        }
        var swagger = await publico.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json", Ct);
        var operacion = swagger.GetProperty("paths").GetProperty("/api/v1/cuentas/{id}/liquidar").GetProperty("post");
        foreach (string codigo in new[] { "200", "400", "401", "403", "404", "409" }) Assert.True(operacion.GetProperty("responses").TryGetProperty(codigo, out _));
        Assert.True(operacion.TryGetProperty("requestBody", out _));
        Assert.Contains(operacion.GetProperty("parameters").EnumerateArray(), p => p.GetProperty("name").GetString() == "Idempotency-Key" && p.GetProperty("in").GetString() == "header" && p.GetProperty("required").GetBoolean());
        Assert.Equal(200m, await db.CuentasFinancieras.Where(c => c.Id == cuenta).Select(c => c.Saldo).SingleAsync(Ct));
    }

    [Theory]
    [InlineData("INACTIVO")]
    [InlineData("PASSWORD")]
    public async Task TokenAnteriorAInhabilitacionNoRetiraFondos(string cambio)
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString); using var admin = factory.CreateClient();
        int actor = await new TicketingApiTestSupport(postgres).LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct);
        await using var db = postgres.CreateContext(); var venta = await ReembolsoFixture.VentaAsync(db, postgres.ConnectionString, factory.QrKey, false, Ct);
        int cuenta = await db.MovimientosFinancieros.Where(m => m.CompraId == venta.Id && m.Tipo == "VENTA").Select(m => m.CuentaId).SingleAsync(Ct);
        if (cambio == "INACTIVO") await db.Usuarios.Where(u => u.Id == actor && u.Estado == "ACTIVO").ExecuteUpdateAsync(s => s.SetProperty(u => u.Estado, "INACTIVO"), Ct);
        else await db.Usuarios.Where(u => u.Id == actor).ExecuteUpdateAsync(s => s.SetProperty(u => u.DebeCambiarPassword, true), Ct);
        using var respuesta = await EnviarAsync(admin, cuenta, 100m, "inhabilitado"); Assert.Contains(respuesta.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
        Assert.Equal(300m, await db.CuentasFinancieras.Where(c => c.Id == cuenta).Select(c => c.Saldo).SingleAsync(Ct));
        Assert.False(await db.MovimientosFinancieros.AnyAsync(m => m.CuentaId == cuenta && m.Tipo == "LIQUIDACION", Ct));
    }
    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, int cuenta, decimal monto, string? clave)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/cuentas/{cuenta}/liquidar") { Content = JsonContent.Create(new { monto }) };
        if (clave is not null) request.Headers.Add("Idempotency-Key", clave);
        return await client.SendAsync(request, Ct);
    }
}
