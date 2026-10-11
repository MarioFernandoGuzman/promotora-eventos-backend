using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Messaging;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Ticketing;
using Promotora.Application.Ticketing.Ports;
using Promotora.Domain.Identity;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Messaging;
using Promotora.Infrastructure.Tests;

namespace Promotora.Api.Tests;

public sealed class CompraOutboxApiTests(PostgresFixture postgres, RabbitMqFixture rabbit) : IClassFixture<PostgresFixture>, IClassFixture<RabbitMqFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CompraHttpProduceOutboxQueRelayEntregaConIdentidadEstable()
    {
        await using var db = postgres.CreateContext();
        var datos = await CompraFixture.CrearAsync(db, 5, Ct);
        await using var factory = new IdentityApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        await new TicketingApiTestSupport(postgres).LoginAsync(client, RolUsuario.CLIENTE, Ct);
        var compra = await TicketingApiTestSupport.ComprarAsync(client, datos.EventoId, datos.TipoId, 2, Ct);
        var row = await db.OutboxMensajes.AsNoTracking().SingleAsync(m => m.AgregadoId == compra.PublicId.ToString("D"), Ct);
        int mensajesCompra = await db.OutboxMensajes.CountAsync(m => !m.Publicado && m.Tipo == TiposMensaje.ProcesarCompra, Ct);
        await using var publisher = new RabbitMqMessagePublisher(rabbit.Options);
        var result = await new EfOutboxRelay(db, publisher, new SystemClock(TimeProvider.System)).PublicarLoteAsync(10, Ct);
        Assert.True(result.Publicados >= 1);
        Assert.Equal(0, result.Fallidos);
        await using var connection = await rabbit.ConectarAsync(Ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: Ct);
        bool encontrado = false;
        for (var i = 0; i < mensajesCompra; i++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var message = await channel.BasicGetAsync(RabbitMqTopology.ComprasQueue, false, timeout.Token);
            while (message is null)
            {
                await Task.Delay(50, timeout.Token);
                message = await channel.BasicGetAsync(RabbitMqTopology.ComprasQueue, false, timeout.Token);
            }
            Assert.NotNull(message);
            if (message.BasicProperties.MessageId == row.Id.ToString(System.Globalization.CultureInfo.InvariantCulture))
            {
                using var payload = System.Text.Json.JsonDocument.Parse(message.Body);
                Assert.Equal(compra.PublicId, payload.RootElement.GetProperty("publicId").GetGuid());
                Assert.Single(payload.RootElement.EnumerateObject());
                encontrado = true;
            }
            await channel.BasicAckAsync(message.DeliveryTag, false, Ct);
        }
        Assert.True(encontrado);
        Assert.Equal("PENDIENTE", await db.Compras.Where(c => c.PublicId == compra.PublicId).Select(c => c.Estado).SingleAsync(Ct));
    }

    [Fact]
    public async Task FalloDespuesDeEscribirOutboxRevierteCompraReservaDetallesYAuditoria()
    {
        await using var db = postgres.CreateContext();
        var datos = await CompraFixture.CrearAsync(db, 5, Ct);
        await using var factory = new IdentityApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var usuario = await new TicketingApiTestSupport(postgres).LoginAsync(client, RolUsuario.CLIENTE, Ct);
        int auditorias = await db.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "compras", Ct);
        await using var scope = factory.Services.CreateAsyncScope();
        var fallo = new FallarOutbox(scope.ServiceProvider.GetRequiredService<IOutboxWriter>());
        var handler = ActivatorUtilities.CreateInstance<CrearCompraHandler>(scope.ServiceProvider, new Actual(usuario), fallo);
        string key = Guid.NewGuid().ToString("N");
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(new(datos.EventoId, [new(datos.TipoId, 2)], key), Ct));
        Assert.False(await db.Compras.AnyAsync(c => c.IdempotencyKey == key, Ct));
        Assert.False(await db.DetalleCompra.AnyAsync(d => d.TipoEntradaId == datos.TipoId, Ct));
        Assert.Equal(5, await db.TiposEntrada.Where(t => t.Id == datos.TipoId).Select(t => t.CupoDisponible).SingleAsync(Ct));
        Assert.False(await db.OutboxMensajes.AnyAsync(m => m.AgregadoId == fallo.PublicId, Ct));
        Assert.Equal(auditorias, await db.LogsAuditoria.CountAsync(a => a.EntidadAfectada == "compras", Ct));
    }

    [Fact]
    public async Task ContratoPreparado202ConLocationYRepeticionSinDuplicarLuegoCancelada200()
    {
        await using var db = postgres.CreateContext();
        var datos = await CompraFixture.CrearAsync(db, 5, Ct);
        await using var baseFactory = new IdentityApiFactory(postgres.ConnectionString);
        await using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<ICompraProcesamientoEstado, DisponibleSoloEnTest>()));
        using var client = factory.CreateClient();
        await new TicketingApiTestSupport(postgres).LoginAsync(client, RolUsuario.CLIENTE, Ct);
        string key = Guid.NewGuid().ToString("N");
        async Task<HttpResponseMessage> ComprarAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/compras")
            { Content = JsonContent.Create(new { eventoId = datos.EventoId, items = new[] { new ItemCompra(datos.TipoId, 1) } }) };
            request.Headers.Add("Idempotency-Key", key);
            return await client.SendAsync(request, Ct);
        }
        using var first = await ComprarAsync();
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var compra = (await first.Content.ReadFromJsonAsync<CompraCreadaDto>(Ct))!;
        Assert.EndsWith($"/api/v1/compras/{compra.PublicId}", first.Headers.Location!.ToString());
        using var repeated = await ComprarAsync();
        Assert.Equal(HttpStatusCode.Accepted, repeated.StatusCode);
        Assert.Equal(first.Headers.Location, repeated.Headers.Location);
        Assert.Equal(1, await db.OutboxMensajes.CountAsync(m => m.AgregadoId == compra.PublicId.ToString("D"), Ct));
        using var poll = await client.GetAsync(first.Headers.Location, Ct);
        Assert.Equal(HttpStatusCode.OK, poll.StatusCode);
        using var cancel = await client.PostAsync($"/api/v1/compras/{compra.PublicId}/cancelar", null, Ct);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        using var terminal = await ComprarAsync();
        Assert.Equal(HttpStatusCode.OK, terminal.StatusCode);
        Assert.Equal("CANCELADA", (await terminal.Content.ReadFromJsonAsync<CompraCreadaDto>(Ct))!.Estado);
    }

    private sealed class DisponibleSoloEnTest : ICompraProcesamientoEstado { public bool Disponible => true; }
    private sealed record Actual(int? UsuarioId) : IUsuarioActual { public string? IpOrigen => null; }
    private sealed class FallarOutbox(IOutboxWriter inner) : IOutboxWriter
    {
        public string? PublicId { get; private set; }
        public async Task<long> EscribirAsync(NuevoMensajeOutbox mensaje, CancellationToken ct)
        {
            PublicId = mensaje.AgregadoId;
            await inner.EscribirAsync(mensaje, ct);
            throw new InvalidOperationException("Fallo simulado después de guardar outbox");
        }
    }
}
