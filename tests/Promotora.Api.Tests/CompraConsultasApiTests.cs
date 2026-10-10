using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared;
using Promotora.Application.Ticketing;
using Promotora.Domain.Identity;
using Promotora.Domain.Shared;
using Promotora.Infrastructure.Tests;

namespace Promotora.Api.Tests;

public sealed class CompraConsultasApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task ConsultaYListaSoloComprasPropiasConPaginacionValidada()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext(); var datos = await CompraFixture.CrearAsync(db, 10, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var uno = factory.CreateClient(); using var dos = factory.CreateClient(); var support = new TicketingApiTestSupport(fixture);
        await support.LoginAsync(uno, RolUsuario.CLIENTE, ct); await support.LoginAsync(dos, RolUsuario.CLIENTE, ct);
        var primera = await TicketingApiTestSupport.ComprarAsync(uno, datos.EventoId, datos.TipoId, 1, ct);
        var segunda = await TicketingApiTestSupport.ComprarAsync(uno, datos.EventoId, datos.TipoId, 2, ct);
        var ajena = await TicketingApiTestSupport.ComprarAsync(dos, datos.EventoId, datos.TipoId, 1, ct);
        using var response = await uno.GetAsync($"/api/v1/compras/{primera.PublicId}", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        var detalle = (await response.Content.ReadFromJsonAsync<CompraCreadaDto>(ct))!;
        Assert.Equal(primera.PublicId, detalle.PublicId); Assert.Equal(primera.Total, detalle.Total);
        Assert.Equal(HttpStatusCode.NotFound, (await uno.GetAsync($"/api/v1/compras/{ajena.PublicId}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await uno.GetAsync($"/api/v1/compras/{Guid.NewGuid()}", ct)).StatusCode);
        var pagina = (await uno.GetFromJsonAsync<PaginaDto<CompraCreadaDto>>("/api/v1/compras?page=1&pageSize=1", ct))!;
        Assert.Equal(2, pagina.Total); Assert.Equal(segunda.PublicId, Assert.Single(pagina.Items).PublicId);
        Assert.Equal(primera.PublicId, Assert.Single((await uno.GetFromJsonAsync<PaginaDto<CompraCreadaDto>>("/api/v1/compras?page=2&pageSize=1", ct))!.Items).PublicId);
        Assert.Equal(HttpStatusCode.BadRequest, (await uno.GetAsync("/api/v1/compras?page=0", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await uno.GetAsync("/api/v1/compras?pageSize=101", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await uno.GetAsync($"/api/v1/compras/{Guid.Empty}", ct)).StatusCode);
    }
    [Fact]
    public async Task MisBoletosIncluyeQrYEstadosSinExponerLosAjenos()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext(); var datos = await CompraFixture.CrearAsync(db, 10, ct);
        await db.TiposEntrada.Where(t => t.Id == datos.TipoId).ExecuteUpdateAsync(s => s.SetProperty(t => t.Precio, 0m), ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var uno = factory.CreateClient(); using var dos = factory.CreateClient(); var support = new TicketingApiTestSupport(fixture);
        await support.LoginAsync(uno, RolUsuario.CLIENTE, ct); await support.LoginAsync(dos, RolUsuario.CLIENTE, ct);
        var propia = await TicketingApiTestSupport.ComprarAsync(uno, datos.EventoId, datos.TipoId, 1, ct);
        var ajena = await TicketingApiTestSupport.ComprarAsync(dos, datos.EventoId, datos.TipoId, 1, ct);
        byte[] key = RandomNumberGenerator.GetBytes(32);
        var original = await TicketingApiTestSupport.BoletoGratisAsync(db, propia.PublicId, key, "ANULADO", ct);
        await TicketingApiTestSupport.BoletoGratisAsync(db, ajena.PublicId, key, "VALIDO", ct);
        using var response = await uno.GetAsync("/api/v1/boletos?page=1&pageSize=1", ct); Assert.True(response.Headers.CacheControl?.NoStore);
        var pagina = (await response.Content.ReadFromJsonAsync<PaginaDto<BoletoClienteDto>>(ct))!; var boleto = Assert.Single(pagina.Items);
        Assert.Equal(1, pagina.Total); Assert.Equal(original.PublicId, boleto.PublicId); Assert.Equal(propia.PublicId, boleto.CompraPublicId);
        Assert.True(original.CodigoQr == boleto.CodigoQr); Assert.Equal("ANULADO", boleto.Estado);
        Assert.Equal(HttpStatusCode.BadRequest, (await uno.GetAsync("/api/v1/boletos?page=-1", ct)).StatusCode);
        Assert.Empty((await uno.GetFromJsonAsync<PaginaDto<BoletoClienteDto>>("/api/v1/boletos?page=2&pageSize=1", ct))!.Items);
    }
    [Fact]
    public async Task VeinteCancelacionesDevuelvenCupoYAuditanUnaSolaVez()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext(); var datos = await CompraFixture.CrearAsync(db, 10, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString); using var client = factory.CreateClient();
        int usuario = await new TicketingApiTestSupport(fixture).LoginAsync(client, RolUsuario.CLIENTE, ct);
        var compra = await TicketingApiTestSupport.ComprarAsync(client, datos.EventoId, datos.TipoId, 3, ct);
        var respuestas = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => client.PostAsync($"/api/v1/compras/{compra.PublicId}/cancelar", null, ct)));
        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal("CANCELADA", (await client.GetFromJsonAsync<CompraCreadaDto>($"/api/v1/compras/{compra.PublicId}", ct))!.Estado);
        Assert.Equal(10, await db.TiposEntrada.Where(t => t.Id == datos.TipoId).Select(t => t.CupoDisponible).SingleAsync(ct));
        int id = await db.Compras.Where(c => c.PublicId == compra.PublicId).Select(c => c.Id).SingleAsync(ct);
        Assert.Equal(1, await db.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "compras" && a.EntidadId == id.ToString() && a.Accion == "UPDATE" && a.UsuarioId == usuario, ct));
        Assert.Equal(3, await db.DetalleCompra.Where(d => d.CompraId == id).SumAsync(d => d.Cantidad, ct));
        Assert.False(await db.Pagos.AnyAsync(p => p.CompraId == id, ct));
        foreach (var response in respuestas) response.Dispose();
    }
    [Theory]
    [InlineData("PROCESANDO")]
    [InlineData("COMPLETADA")]
    [InlineData("EXPIRADA")]
    public async Task CancelacionAjenaONoPendienteNoDevuelveCupo(string estado)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext(); var datos = await CompraFixture.CrearAsync(db, 10, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var propietario = factory.CreateClient(); using var otro = factory.CreateClient(); var support = new TicketingApiTestSupport(fixture);
        await support.LoginAsync(propietario, RolUsuario.CLIENTE, ct); await support.LoginAsync(otro, RolUsuario.CLIENTE, ct);
        var compra = await TicketingApiTestSupport.ComprarAsync(propietario, datos.EventoId, datos.TipoId, 2, ct);
        Assert.Equal(HttpStatusCode.NotFound, (await otro.PostAsync($"/api/v1/compras/{compra.PublicId}/cancelar", null, ct)).StatusCode);
        // Estado existente en fixture temporal; no habilita finalización de producción.
        await db.Compras.Where(c => c.PublicId == compra.PublicId && c.Estado == "PENDIENTE").ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, estado), ct);
        Assert.Equal(HttpStatusCode.Conflict, (await propietario.PostAsync($"/api/v1/compras/{compra.PublicId}/cancelar", null, ct)).StatusCode);
        Assert.Equal(8, await db.TiposEntrada.Where(t => t.Id == datos.TipoId).Select(t => t.CupoDisponible).SingleAsync(ct));
    }
    [Fact]
    public async Task FalloDeAuditoriaRevierteCancelacionYCupo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext(); var datos = await CompraFixture.CrearAsync(db, 10, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString); using var client = factory.CreateClient();
        int usuario = await new TicketingApiTestSupport(fixture).LoginAsync(client, RolUsuario.CLIENTE, ct);
        var compra = await TicketingApiTestSupport.ComprarAsync(client, datos.EventoId, datos.TipoId, 2, ct);
        await using var scope = factory.Services.CreateAsyncScope(); var actual = new Actual(usuario);
        var access = ActivatorUtilities.CreateInstance<ClienteCompraAccess>(scope.ServiceProvider, actual);
        var handler = ActivatorUtilities.CreateInstance<CancelarCompraHandler>(scope.ServiceProvider, access, actual, new AuditFallando());
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(new(compra.PublicId), ct));
        Assert.Equal("PENDIENTE", await db.Compras.Where(c => c.PublicId == compra.PublicId).Select(c => c.Estado).SingleAsync(ct));
        Assert.Equal(8, await db.TiposEntrada.Where(t => t.Id == datos.TipoId).Select(t => t.CupoDisponible).SingleAsync(ct));
    }
    [Fact]
    public async Task AuthYHandlersRechazanActoresNoClientesOInactivos()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory(fixture.ConnectionString); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/compras", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/boletos", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync($"/api/v1/compras/{Guid.NewGuid()}/cancelar", null, ct)).StatusCode);
        int usuario = await new TicketingApiTestSupport(fixture).LoginAsync(client, RolUsuario.ADMINISTRADOR, ct);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/compras", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/boletos", ct)).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope(); var actual = new Actual(usuario);
        var access = ActivatorUtilities.CreateInstance<ClienteCompraAccess>(scope.ServiceProvider, actual);
        var handler = ActivatorUtilities.CreateInstance<ObtenerCompraHandler>(scope.ServiceProvider, access);
        Assert.Equal(ErrorType.Forbidden, Assert.Single((await handler.HandleAsync(new(Guid.NewGuid()), ct)).Errors).Type);
        usuario = await new TicketingApiTestSupport(fixture).LoginAsync(client, RolUsuario.CLIENTE, ct);
        await using var db = fixture.CreateContext();
        await db.Usuarios.Where(u => u.Id == usuario && u.Estado == "ACTIVO").ExecuteUpdateAsync(s => s.SetProperty(u => u.Estado, "INACTIVO"), ct);
        access = ActivatorUtilities.CreateInstance<ClienteCompraAccess>(scope.ServiceProvider, new Actual(usuario)); Assert.Null(await access.ClienteAsync(false, ct));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/boletos", ct)).StatusCode);
    }
    private sealed record Actual(int? UsuarioId) : IUsuarioActual { public string? IpOrigen => null; }
    private sealed class AuditFallando : IAuditLogger
    {
        public Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, CancellationToken ct) => throw new InvalidOperationException("Fallo de auditoría de prueba.");
        public Task WriteAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? ip, DetalleAuditoria? detalle, CancellationToken ct) => throw new InvalidOperationException("Fallo de auditoría de prueba.");
    }
}
