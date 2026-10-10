using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Ticketing;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Shared.Persistence;
using Promotora.Infrastructure.Tests;

namespace Promotora.Api.Tests;

public sealed class CompraIdempotenciaApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task CuarentaSolicitudesPropiasConUnaClaveCreanUnaCompraYDescuentanUnaVez()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext(); var datos = await CompraFixture.CrearAsync(db, 10, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString); using var cliente = factory.CreateClient();
        await new TicketingApiTestSupport(fixture).LoginAsync(cliente, RolUsuario.CLIENTE, ct); string key = Guid.NewGuid().ToString();
        var respuestas = await ConcurrentesAsync(40, _ => ComprarAsync(cliente, datos.EventoId, datos.TipoId, 3, key, ct), ct);
        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.Created)); Assert.Equal(39, respuestas.Count(r => r.StatusCode == HttpStatusCode.OK));
        var compras = await Task.WhenAll(respuestas.Select(r => r.Content.ReadFromJsonAsync<CompraCreadaDto>(ct)));
        Guid publicId = compras[0]!.PublicId;
        Assert.All(compras, c => { Assert.NotNull(c); Assert.Equal(publicId, c.PublicId); Assert.Equal(369.60m, c.Total); Assert.Equal(3, Assert.Single(c.Items).Cantidad); });
        await VerificarUnaCompraAsync(db, key, datos.TipoId, 7, ct);
        foreach (var response in respuestas) response.Dispose();
    }

    [Fact]
    public async Task DosClientesDisputanUnaClaveSinAccederALaCompraAjena()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext(); var datos = await CompraFixture.CrearAsync(db, 10, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var uno = factory.CreateClient(); using var dos = factory.CreateClient(); var support = new TicketingApiTestSupport(fixture);
        await support.LoginAsync(uno, RolUsuario.CLIENTE, ct); await support.LoginAsync(dos, RolUsuario.CLIENTE, ct);
        string key = Guid.NewGuid().ToString();
        var respuestas = await ConcurrentesAsync(40, i => ComprarAsync(i % 2 == 0 ? uno : dos, datos.EventoId, datos.TipoId, 3, key, ct), ct);
        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.Created)); Assert.Equal(19, respuestas.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(20, respuestas.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        var ganadora = (await respuestas.Single(r => r.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<CompraCreadaDto>(ct))!;
        int indiceGanador = Array.FindIndex(respuestas, r => r.StatusCode == HttpStatusCode.Created);
        var perdedor = indiceGanador % 2 == 0 ? dos : uno;
        Assert.Equal(HttpStatusCode.NotFound, (await perdedor.GetAsync($"/api/v1/compras/{ganadora.PublicId}", ct)).StatusCode);
        foreach (var response in respuestas.Where(r => r.StatusCode == HttpStatusCode.Conflict))
        {
            string problem = await response.Content.ReadAsStringAsync(ct);
            Assert.DoesNotContain(ganadora.PublicId.ToString(), problem);
        }
        await VerificarUnaCompraAsync(db, key, datos.TipoId, 7, ct);
        foreach (var response in respuestas) response.Dispose();
    }

    [Fact]
    public async Task ReutilizarClaveCanceladaConPrecioNuevoConservaMontosYNoReserva()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext(); var datos = await CompraFixture.CrearAsync(db, 10, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString); using var cliente = factory.CreateClient();
        await new TicketingApiTestSupport(fixture).LoginAsync(cliente, RolUsuario.CLIENTE, ct); string key = Guid.NewGuid().ToString();
        using var creada = await ComprarAsync(cliente, datos.EventoId, datos.TipoId, 3, key, ct); Assert.Equal(HttpStatusCode.Created, creada.StatusCode);
        var original = (await creada.Content.ReadFromJsonAsync<CompraCreadaDto>(ct))!;
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsync($"/api/v1/compras/{original.PublicId}/cancelar", null, ct)).StatusCode);
        await db.TiposEntrada.Where(t => t.Id == datos.TipoId).ExecuteUpdateAsync(s => s.SetProperty(t => t.Precio, 120m), ct);
        using var repetida = await ComprarAsync(cliente, datos.EventoId, datos.TipoId, 1, key, ct); Assert.Equal(HttpStatusCode.OK, repetida.StatusCode);
        var compra = (await repetida.Content.ReadFromJsonAsync<CompraCreadaDto>(ct))!;
        Assert.Equal(original.PublicId, compra.PublicId); Assert.Equal("CANCELADA", compra.Estado); Assert.Equal(original.Total, compra.Total);
        Assert.Equal(3, Assert.Single(compra.Items).Cantidad); Assert.Equal(100m, compra.Items[0].PrecioUnitario);
        await VerificarUnaCompraAsync(db, key, datos.TipoId, 10, ct);
        int id = await db.Compras.Where(c => c.IdempotencyKey == key).Select(c => c.Id).SingleAsync(ct);
        Assert.Equal(1, await db.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "compras" && a.EntidadId == id.ToString() && a.Accion == "UPDATE", ct));
    }

    [Fact]
    public async Task ConflictoSinCupoNoConsumeClaveYReintentoTrasCancelacionCreaUnaCompra()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateContext(); var datos = await CompraFixture.CrearAsync(db, 5, ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString);
        using var uno = factory.CreateClient(); using var dos = factory.CreateClient(); var support = new TicketingApiTestSupport(fixture);
        await support.LoginAsync(uno, RolUsuario.CLIENTE, ct); await support.LoginAsync(dos, RolUsuario.CLIENTE, ct);
        var reservada = await TicketingApiTestSupport.ComprarAsync(uno, datos.EventoId, datos.TipoId, 3, ct); string key = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.Conflict, (await ComprarAsync(dos, datos.EventoId, datos.TipoId, 4, key, ct)).StatusCode);
        Assert.False(await db.Compras.AnyAsync(c => c.IdempotencyKey == key, ct));
        Assert.Equal(HttpStatusCode.OK, (await uno.PostAsync($"/api/v1/compras/{reservada.PublicId}/cancelar", null, ct)).StatusCode);
        using var reintento = await ComprarAsync(dos, datos.EventoId, datos.TipoId, 4, key, ct); Assert.Equal(HttpStatusCode.Created, reintento.StatusCode);
        using var repetida = await ComprarAsync(dos, datos.EventoId, datos.TipoId, 4, key, ct); Assert.Equal(HttpStatusCode.OK, repetida.StatusCode);
        Assert.Equal((await reintento.Content.ReadFromJsonAsync<CompraCreadaDto>(ct))!.PublicId, (await repetida.Content.ReadFromJsonAsync<CompraCreadaDto>(ct))!.PublicId);
        await VerificarUnaCompraAsync(db, key, datos.TipoId, 1, ct);
    }

    private static async Task VerificarUnaCompraAsync(PromotoraDbContext db, string key, int tipoId, int cupoEsperado, CancellationToken ct)
    {
        var compra = Assert.Single(await db.Compras.AsNoTracking().Where(c => c.IdempotencyKey == key).ToArrayAsync(ct));
        Assert.Equal(1, await db.DetalleCompra.CountAsync(d => d.CompraId == compra.Id, ct));
        Assert.Equal(cupoEsperado, await db.TiposEntrada.Where(t => t.Id == tipoId).Select(t => t.CupoDisponible).SingleAsync(ct));
        Assert.Equal(1, await db.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "compras" && a.EntidadId == compra.Id.ToString() && a.Accion == "CREATE", ct));
        Assert.False(await db.Pagos.AnyAsync(p => p.CompraId == compra.Id, ct));
        Assert.False(await db.MovimientosFinancieros.AnyAsync(m => m.CompraId == compra.Id, ct));
    }
    private static async Task<HttpResponseMessage[]> ConcurrentesAsync(int n, Func<int, Task<HttpResponseMessage>> enviar, CancellationToken ct)
    {
        var salida = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tareas = Enumerable.Range(0, n).Select(async i => { await salida.Task.WaitAsync(ct); return await enviar(i); }).ToArray();
        salida.SetResult(); return await Task.WhenAll(tareas);
    }
    private static async Task<HttpResponseMessage> ComprarAsync(HttpClient cliente, int eventoId, int tipoId, int cantidad, string key, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/compras")
        { Content = JsonContent.Create(new { eventoId, items = new[] { new ItemCompra(tipoId, cantidad) } }) };
        request.Headers.Add("Idempotency-Key", key); return await cliente.SendAsync(request, ct);
    }
}
