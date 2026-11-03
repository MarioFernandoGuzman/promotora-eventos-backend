using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Finance;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Finance.Persistence;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Tests;

namespace Promotora.Api.Tests;

public sealed class CuentasApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DuenoCierraSuCuentaYRepiteMismaRespuestaSinDuplicarAuditoria()
    {
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        var usuarioId = await new TicketingApiTestSupport(fixture).LoginAsync(client, RolUsuario.DUENO_RECINTO, Ct);
        await using var db = fixture.CreateContext();
        int id = await CuentaIdAsync(db, usuarioId);
        using var response = await client.PostAsync($"/api/v1/cuentas/{id}/cerrar", null, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cuenta = (await response.Content.ReadFromJsonAsync<CuentaDto>(Ct))!;
        Assert.Equal("CERRADA", cuenta.Estado); Assert.Equal(0m, cuenta.Saldo);
        Assert.NotNull(cuenta.FechaCierre); Assert.Equal(DateTimeKind.Utc, cuenta.FechaCierre.Value.Kind);
        using var repetida = await client.PostAsync($"/api/v1/cuentas/{id}/cerrar", null, Ct);
        Assert.Equal(HttpStatusCode.OK, repetida.StatusCode);
        Assert.Equal(cuenta, await repetida.Content.ReadFromJsonAsync<CuentaDto>(Ct));
        Assert.Equal(1, await db.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "cuentas_financieras" && a.EntidadId == id.ToString() && a.Accion == "UPDATE", Ct));
        using var invalid = await client.PostAsync("/api/v1/cuentas/0/cerrar", null, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var swagger = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json", Ct);
        var operation = swagger.GetProperty("paths").GetProperty("/api/v1/cuentas/{id}/cerrar").GetProperty("post");
        Assert.True(operation.TryGetProperty("security", out _));
        Assert.False(operation.TryGetProperty("requestBody", out _));
        foreach (string code in new[] { "200", "400", "401", "403", "404", "409" })
            Assert.True(operation.GetProperty("responses").TryGetProperty(code, out _));
    }

    [Fact]
    public async Task CierreExigeSesionRolYPropiedadInclusoConJwtPrevioABaja()
    {
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        using var anonima = await client.PostAsync("/api/v1/cuentas/1/cerrar", null, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonima.StatusCode);
        var support = new TicketingApiTestSupport(fixture);
        await support.LoginAsync(client, RolUsuario.CLIENTE, Ct);
        using var cliente = await client.PostAsync("/api/v1/cuentas/1/cerrar", null, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, cliente.StatusCode);
        int propio = await support.LoginAsync(client, RolUsuario.DUENO_RECINTO, Ct);
        await using var db = fixture.CreateContext(); int id = await CuentaIdAsync(db, propio);
        int ajeno = await support.LoginAsync(client, RolUsuario.DUENO_RECINTO, Ct);
        using var foreign = await client.PostAsync($"/api/v1/cuentas/{id}/cerrar", null, Ct);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var missing = await client.PostAsync("/api/v1/cuentas/2147483647/cerrar", null, Ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        int idAjeno = await CuentaIdAsync(db, ajeno);
        await db.Usuarios.Where(u => u.Id == ajeno && u.Estado == "ACTIVO")
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Estado, "INACTIVO"), Ct);
        using var disabled = await client.PostAsync($"/api/v1/cuentas/{idAjeno}/cerrar", null, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, disabled.StatusCode);
        Assert.Equal("ACTIVA", await db.CuentasFinancieras.Where(c => c.Id == idAjeno).Select(c => c.Estado).SingleAsync(Ct));
    }

    [Fact]
    public async Task AdminCierraDuenoPeroNoCuentasDelSistema()
    {
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(); var support = new TicketingApiTestSupport(fixture);
        int dueno = await support.LoginAsync(client, RolUsuario.DUENO_RECINTO, Ct);
        await support.LoginAsync(client, RolUsuario.ADMINISTRADOR, Ct);
        await using var db = fixture.CreateContext(); int id = await CuentaIdAsync(db, dueno);
        using var cierre = await client.PostAsync($"/api/v1/cuentas/{id}/cerrar", null, Ct);
        Assert.Equal(HttpStatusCode.OK, cierre.StatusCode);
        foreach (string tipo in new[] { "PLATAFORMA", "IMPUESTOS" })
        {
            int sistema = await db.CuentasFinancieras.Where(c => c.Tipo == tipo && c.Moneda == "GTQ").Select(c => c.Id).SingleAsync(Ct);
            using var response = await client.PostAsync($"/api/v1/cuentas/{sistema}/cerrar", null, Ct);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public async Task SaldoPositivoONegativoDevuelve409YConservaCuenta(int monto)
    {
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        int dueno = await new TicketingApiTestSupport(fixture).LoginAsync(client, RolUsuario.DUENO_RECINTO, Ct);
        await using var db = fixture.CreateContext(); int id = await CuentaIdAsync(db, dueno);
        // Fixture coherente: movimiento y saldo se preparan juntos en PostgreSQL temporal.
        await using (var tx = await db.Database.BeginTransactionAsync(Ct))
        {
            db.MovimientosFinancieros.Add(new MovimientoFinancieroRow { CuentaId = id, Tipo = "LIQUIDACION", Monto = monto });
            await db.SaveChangesAsync(Ct);
            await db.CuentasFinancieras.Where(c => c.Id == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Saldo, monto), Ct);
            await tx.CommitAsync(Ct);
        }
        using var response = await client.PostAsync($"/api/v1/cuentas/{id}/cerrar", null, Ct);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ACTIVA", await db.CuentasFinancieras.Where(c => c.Id == id).Select(c => c.Estado).SingleAsync(Ct));
        Assert.False(await db.LogsAuditoria.AnyAsync(a => a.EntidadAfectada == "cuentas_financieras" && a.EntidadId == id.ToString() && a.Accion == "UPDATE", Ct));
    }

    private static Task<int> CuentaIdAsync(PromotoraDbContext db, int usuarioId) => db.CuentasFinancieras
        .Where(c => c.DuenoId == db.DuenosRecinto.Where(d => d.UsuarioId == usuarioId).Select(d => d.Id).Single())
        .Select(c => c.Id).SingleAsync(Ct);
}
