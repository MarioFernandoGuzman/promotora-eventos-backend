using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Finance;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Tests;

namespace Promotora.Api.Tests;

public sealed class ReembolsosApiTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ContratoAdmin200RepetidoYErrores401403404400409ConSwagger()
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString);
        using var publico = factory.CreateClient(); using var cliente = factory.CreateClient(); using var dueno = factory.CreateClient(); using var admin = factory.CreateClient();
        var soporte = new TicketingApiTestSupport(postgres);
        await soporte.LoginAsync(cliente, RolUsuario.CLIENTE, Ct); await soporte.LoginAsync(dueno, RolUsuario.DUENO_RECINTO, Ct); await soporte.LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct);
        await using var db = postgres.CreateContext(); var venta = await ReembolsoFixture.VentaAsync(db, postgres.ConnectionString, factory.QrKey, false, Ct);
        string ruta = $"/api/v1/compras/{venta.PublicId}/reembolsar";
        using var anonima = await publico.PostAsync(ruta, null, Ct); Assert.Equal(HttpStatusCode.Unauthorized, anonima.StatusCode);
        foreach (var actor in new[] { cliente, dueno }) { using var prohibida = await actor.PostAsync(ruta, null, Ct); Assert.Equal(HttpStatusCode.Forbidden, prohibida.StatusCode); }
        using var primero = await admin.PostAsync(ruta, null, Ct); Assert.Equal(HttpStatusCode.OK, primero.StatusCode);
        var payload = (await primero.Content.ReadFromJsonAsync<ReembolsoDto>(Ct))!;
        Assert.Equal(new(venta.PublicId, "REEMBOLSADO", 369.60m, "GTQ"), payload);
        using var repetida = await admin.PostAsync(ruta, null, Ct); Assert.Equal(HttpStatusCode.OK, repetida.StatusCode);
        Assert.Equal(payload, await repetida.Content.ReadFromJsonAsync<ReembolsoDto>(Ct));
        using var inexistente = await admin.PostAsync($"/api/v1/compras/{Guid.NewGuid()}/reembolsar", null, Ct); Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        using var vacia = await admin.PostAsync($"/api/v1/compras/{Guid.Empty}/reembolsar", null, Ct); Assert.Equal(HttpStatusCode.BadRequest, vacia.StatusCode);
        var datos = await CompraFixture.CrearAsync(db, 5, Ct);
        var pendiente = await TicketingApiTestSupport.ComprarAsync(cliente, datos.EventoId, datos.TipoId, 1, Ct);
        using var conflicto = await admin.PostAsync($"/api/v1/compras/{pendiente.PublicId}/reembolsar", null, Ct); Assert.Equal(HttpStatusCode.Conflict, conflicto.StatusCode);
        Assert.Equal("application/problem+json", conflicto.Content.Headers.ContentType!.MediaType);
        var swagger = await publico.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json", Ct);
        var operacion = swagger.GetProperty("paths").GetProperty("/api/v1/compras/{publicId}/reembolsar").GetProperty("post");
        foreach (string codigo in new[] { "200", "400", "401", "403", "404", "409" }) Assert.True(operacion.GetProperty("responses").TryGetProperty(codigo, out _));
        Assert.False(operacion.TryGetProperty("requestBody", out _));
        Assert.Equal(1, await db.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "compras" && a.EntidadId == venta.Id.ToString() && a.Accion == "REFUND", Ct));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task EscaneoYReembolsoConcurrentesConDosAdministradoresCoordinanElCupo(int ronda)
    {
        _ = ronda;
        await using var factory = new IdentityApiFactory(postgres.ConnectionString); using var escaner = factory.CreateClient(); using var finanzas = factory.CreateClient();
        var soporte = new TicketingApiTestSupport(postgres);
        await soporte.LoginAsync(escaner, RolUsuario.ADMINISTRADOR, Ct); await soporte.LoginAsync(finanzas, RolUsuario.ADMINISTRADOR, Ct);
        await using var db = postgres.CreateContext(); var venta = await ReembolsoFixture.VentaAsync(db, postgres.ConnectionString, factory.QrKey, false, Ct);
        string qr = await db.Boletos.Where(b => b.DetalleCompraId == venta.DetalleId).OrderBy(b => b.Id).Select(b => b.CodigoQr).FirstAsync(Ct);
        var salida = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpStatusCode> EscanearAsync()
        {
            await salida.Task.WaitAsync(Ct); using var response = await escaner.PostAsJsonAsync("/api/v1/boletos/validar", new { codigoQr = qr }, Ct); return response.StatusCode;
        }
        async Task<HttpStatusCode> ReembolsarAsync()
        {
            await salida.Task.WaitAsync(Ct); using var response = await finanzas.PostAsync($"/api/v1/compras/{venta.PublicId}/reembolsar", null, Ct); return response.StatusCode;
        }
        var escaneos = Enumerable.Range(0, 10).Select(_ => EscanearAsync()).ToArray(); var reembolso = ReembolsarAsync();
        salida.SetResult(); var resultados = await Task.WhenAll(escaneos); Assert.Equal(HttpStatusCode.OK, await reembolso);
        Assert.All(resultados, r => Assert.Contains(r, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        int usados = resultados.Count(r => r == HttpStatusCode.OK); Assert.InRange(usados, 0, 1);
        Assert.Equal(usados, await db.Boletos.CountAsync(b => b.DetalleCompraId == venta.DetalleId && b.Estado == "USADO", Ct));
        Assert.Equal(3 - usados, await db.Boletos.CountAsync(b => b.DetalleCompraId == venta.DetalleId && b.Estado == "ANULADO", Ct));
        Assert.Equal(5 - usados, await db.TiposEntrada.Where(t => t.Id == venta.TipoId).Select(t => t.CupoDisponible).SingleAsync(Ct));
        Assert.Equal(6, await db.MovimientosFinancieros.CountAsync(m => m.CompraId == venta.Id, Ct));
        Assert.Equal(0m, await db.MovimientosFinancieros.Where(m => m.CompraId == venta.Id).SumAsync(m => m.Monto, Ct));
        Assert.Equal("REEMBOLSADO", await db.Pagos.Where(p => p.CompraId == venta.Id).Select(p => p.Estado).SingleAsync(Ct));
    }

    [Theory]
    [InlineData("INACTIVO")]
    [InlineData("PASSWORD")]
    public async Task TokenEmitidoAntesDeInhabilitarAdminNoPermiteReembolso(string cambio)
    {
        await using var factory = new IdentityApiFactory(postgres.ConnectionString); using var admin = factory.CreateClient();
        int actor = await new TicketingApiTestSupport(postgres).LoginAsync(admin, RolUsuario.ADMINISTRADOR, Ct);
        await using var db = postgres.CreateContext(); var venta = await ReembolsoFixture.VentaAsync(db, postgres.ConnectionString, factory.QrKey, false, Ct);
        if (cambio == "INACTIVO") await db.Usuarios.Where(u => u.Id == actor && u.Estado == "ACTIVO").ExecuteUpdateAsync(s => s.SetProperty(u => u.Estado, "INACTIVO"), Ct);
        else await db.Usuarios.Where(u => u.Id == actor).ExecuteUpdateAsync(s => s.SetProperty(u => u.DebeCambiarPassword, true), Ct);
        using var response = await admin.PostAsync($"/api/v1/compras/{venta.PublicId}/reembolsar", null, Ct);
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
        Assert.Equal("APROBADO", await db.Pagos.Where(p => p.CompraId == venta.Id).Select(p => p.Estado).SingleAsync(Ct));
        Assert.False(await db.MovimientosFinancieros.AnyAsync(m => m.CompraId == venta.Id && m.Tipo == "REEMBOLSO", Ct));
    }
}
