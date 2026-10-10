using System.Text;
using Promotora.Application.Shared.Messaging;
using Promotora.Infrastructure.Shared.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Promotora.Infrastructure.Tests;

public sealed class RabbitMqMessagingTests(RabbitMqFixture fixture) : IClassFixture<RabbitMqFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TopologiaDurableYRoutingDeLosTresTiposConMensajePersistente()
    {
        await using var publisher = new RabbitMqMessagePublisher(fixture.Options);
        await publisher.PrepararAsync(Ct);
        await using var connection = await fixture.ConectarAsync(Ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: Ct);
        await RabbitMqTopology.DeclararAsync(channel, Ct); // Declaración durable idempotente.
        foreach (var (tipo, queue, id) in new[]
        {
            (TiposMensaje.ProcesarCompra, RabbitMqTopology.ComprasQueue, 1L),
            (TiposMensaje.CompraCompletada, "notificaciones.compra", 2L),
            (TiposMensaje.UsuarioRegistrado, "notificaciones.registro", 3L)
        })
        {
            await publisher.PublicarAsync(new(id, tipo, Encoding.UTF8.GetBytes("{\"publicId\":\"prueba\"}")), Ct);
            var result = await channel.BasicGetAsync(queue, autoAck: false, Ct);
            Assert.NotNull(result);
            Assert.Equal(id.ToString(System.Globalization.CultureInfo.InvariantCulture), result.BasicProperties.MessageId);
            Assert.True(result.BasicProperties.Persistent);
            Assert.Equal("application/json", result.BasicProperties.ContentType);
            Assert.Equal(tipo, result.RoutingKey);
            Assert.Equal("{\"publicId\":\"prueba\"}", Encoding.UTF8.GetString(result.Body.Span));
            await channel.BasicAckAsync(result.DeliveryTag, multiple: false, Ct);
        }
    }

    [Fact]
    public async Task RechazoDeCompraLlegaADlqYRetryExpiradoRegresaAlExchange()
    {
        await using var connection = await fixture.ConectarAsync(Ct);
        await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(true, true), Ct);
        await RabbitMqTopology.DeclararAsync(channel, Ct);
        await channel.BasicPublishAsync(RabbitMqTopology.Exchange, TiposMensaje.ProcesarCompra, true,
            new BasicProperties { Persistent = true, MessageId = "dlq" }, Encoding.UTF8.GetBytes("{}"), Ct);
        var original = await channel.BasicGetAsync(RabbitMqTopology.ComprasQueue, false, Ct);
        Assert.NotNull(original);
        await channel.BasicRejectAsync(original.DeliveryTag, requeue: false, Ct);
        var dlq = await EsperarAsync(channel, RabbitMqTopology.DeadLetterQueue);
        Assert.Equal("dlq", dlq.BasicProperties.MessageId);
        await channel.BasicPublishAsync("", RabbitMqTopology.RetryQueue, true,
            new BasicProperties { Persistent = true, MessageId = "retry", Expiration = "100", Headers = new Dictionary<string, object?> { ["x-intentos"] = 1 } }, Encoding.UTF8.GetBytes("{}"), Ct);
        var retry = await EsperarAsync(channel, RabbitMqTopology.ComprasQueue);
        Assert.Equal("retry", retry.BasicProperties.MessageId);
        Assert.Equal(1, retry.BasicProperties.Headers!["x-intentos"]);
    }

    [Fact]
    public async Task PublisherConcurrenteMantieneIdentidadesSinPerderConfirmaciones()
    {
        await using var publisher = new RabbitMqMessagePublisher(fixture.Options);
        await Task.WhenAll(Enumerable.Range(10, 20).Select(id => publisher.PublicarAsync(new(id, TiposMensaje.UsuarioRegistrado, Encoding.UTF8.GetBytes("{}")), Ct)));
        await using var connection = await fixture.ConectarAsync(Ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: Ct);
        var ids = new HashSet<string>();
        for (var i = 0; i < 20; i++)
        {
            var result = await channel.BasicGetAsync("notificaciones.registro", true, Ct);
            Assert.NotNull(result);
            Assert.True(ids.Add(result.BasicProperties.MessageId!));
        }
        Assert.Equal(20, ids.Count);
    }

    [Fact]
    public async Task MensajeSinBindingEsRechazadoAunqueElBrokerConfirme()
    {
        await using var publisher = new RabbitMqMessagePublisher(fixture.Options);
        await publisher.PrepararAsync(Ct);
        await using var connection = await fixture.ConectarAsync(Ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: Ct);
        await channel.QueueUnbindAsync("notificaciones.compra", RabbitMqTopology.Exchange, TiposMensaje.CompraCompletada, cancellationToken: Ct);
        try
        {
            await Assert.ThrowsAsync<PublishReturnException>(() => publisher.PublicarAsync(new(100, TiposMensaje.CompraCompletada, Encoding.UTF8.GetBytes("{}")), Ct));
        }
        finally { await RabbitMqTopology.DeclararAsync(channel, Ct); }
    }

    [Fact]
    public async Task TipoNoPermitidoYCancelacionNoPublican()
    {
        await using var publisher = new RabbitMqMessagePublisher(fixture.Options);
        await Assert.ThrowsAsync<ArgumentException>(() => publisher.PublicarAsync(new(1, "invalido", Encoding.UTF8.GetBytes("{}")), Ct));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publisher.PublicarAsync(new(1, TiposMensaje.ProcesarCompra, Encoding.UTF8.GetBytes("{}")), cancelled.Token));
        Assert.Throws<InvalidOperationException>(() => new RabbitMqOptions { Host = "localhost", Username = "", Password = "" }.Validar());
    }

    private static async Task<BasicGetResult> EsperarAsync(IChannel channel, string queue)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (true)
        {
            var result = await channel.BasicGetAsync(queue, true, timeout.Token);
            if (result is not null) return result;
            await Task.Delay(50, timeout.Token);
        }
    }
}
