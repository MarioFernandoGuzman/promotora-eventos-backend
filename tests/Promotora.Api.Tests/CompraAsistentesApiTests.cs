using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Promotora.Application.Ticketing;
using Promotora.Domain.Identity;
using Promotora.Domain.Ticketing;
using Promotora.Infrastructure.Tests;

namespace Promotora.Api.Tests;

public sealed class CompraAsistentesApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task CarreraRechazaAsistentesAusentesIncompletosOExtraInvalidoSinReservar(int caso)
    {
        await using var db = fixture.CreateContext(); var datos = await CompraFixture.CrearAsync(db, 5, Ct);
        await db.Eventos.Where(e => e.Id == datos.EventoId).ExecuteUpdateAsync(s => s.SetProperty(e => e.CategoriaId, 5), Ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString); using var client = factory.CreateClient();
        await new TicketingApiTestSupport(fixture).LoginAsync(client, RolUsuario.CLIENTE, Ct);
        IReadOnlyList<DatosAsistente>? personas = caso switch { 0 => null, 1 => [new("Nombre", null)], _ => [new("Nombre", "DOC", "no-json")] };
        string key = Guid.NewGuid().ToString();
        using var request = Solicitud(datos.EventoId, [new(datos.TipoId, 1, personas)], key);
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(caso == 2 ? HttpStatusCode.BadRequest : HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(5, await db.TiposEntrada.Where(t => t.Id == datos.TipoId).Select(t => t.CupoDisponible).SingleAsync(Ct));
        Assert.False(await db.Compras.AnyAsync(c => c.IdempotencyKey == key, Ct));
    }

    [Fact]
    public async Task AgrupaAsistentesPorTipoYLaRepeticionNoCambiaLosDatosCongelados()
    {
        await using var db = fixture.CreateContext(); var datos = await CompraFixture.CrearAsync(db, 5, Ct);
        await db.Eventos.Where(e => e.Id == datos.EventoId).ExecuteUpdateAsync(s => s.SetProperty(e => e.CategoriaId, 5), Ct);
        await using var factory = new IdentityApiFactory(fixture.ConnectionString); using var client = factory.CreateClient();
        await new TicketingApiTestSupport(fixture).LoginAsync(client, RolUsuario.CLIENTE, Ct);
        string key = Guid.NewGuid().ToString();
        using var request = Solicitud(datos.EventoId, [new(datos.TipoId, 1, [new("Persona 1", "DOC1")]), new(datos.TipoId, 1, [new("Persona 2", "DOC2")])], key);
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var compra = (await response.Content.ReadFromJsonAsync<CompraCreadaDto>(Ct))!;
        using var repeat = Solicitud(datos.EventoId, [new(datos.TipoId, 1)], key);
        using var repeated = await client.SendAsync(repeat, Ct);
        Assert.Equal(HttpStatusCode.Accepted, repeated.StatusCode);
        var outbox = await db.OutboxMensajes.SingleAsync(o => o.AgregadoId == compra.PublicId.ToString("D"), Ct);
        var item = Assert.Single(outbox.Payload.RootElement.GetProperty("asistentes").EnumerateArray());
        Assert.Equal(datos.TipoId, item.GetProperty("tipoEntradaId").GetInt32());
        Assert.Equal(2, item.GetProperty("personas").GetArrayLength());
        Assert.Equal(3, await db.TiposEntrada.Where(t => t.Id == datos.TipoId).Select(t => t.CupoDisponible).SingleAsync(Ct));
    }

    private static HttpRequestMessage Solicitud(int eventoId, IReadOnlyList<ItemCompra> items, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/compras") { Content = JsonContent.Create(new { eventoId, items }) };
        request.Headers.Add("Idempotency-Key", key); return request;
    }
}
