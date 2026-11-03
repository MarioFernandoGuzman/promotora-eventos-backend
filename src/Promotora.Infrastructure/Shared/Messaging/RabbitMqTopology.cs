using Promotora.Application.Shared.Messaging;
using RabbitMQ.Client;

namespace Promotora.Infrastructure.Shared.Messaging;

public static class RabbitMqTopology
{
    public const string Exchange = "promotora.compras";
    public const string DeadLetterExchange = "promotora.dlx";
    public const string ComprasQueue = "compras.procesar";
    public const string RetryQueue = "compras.procesar.retry";
    public const string DeadLetterQueue = "compras.procesar.dlq";

    public static async Task DeclararAsync(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: ct);
        await channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: ct);
        await channel.QueueDeclareAsync(ComprasQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = DeadLetterExchange, ["x-dead-letter-routing-key"] = TiposMensaje.ProcesarCompra }, cancellationToken: ct);
        await channel.QueueBindAsync(ComprasQueue, Exchange, TiposMensaje.ProcesarCompra, cancellationToken: ct);
        foreach (var (queue, tipo) in new[] { ("notificaciones.compra", TiposMensaje.CompraCompletada), ("notificaciones.registro", TiposMensaje.UsuarioRegistrado) })
        {
            await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
            await channel.QueueBindAsync(queue, Exchange, tipo, cancellationToken: ct);
        }
        // El consumidor asignará Expiration por mensaje según el intento (backoff, F6-04).
        await channel.QueueDeclareAsync(RetryQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = Exchange, ["x-dead-letter-routing-key"] = TiposMensaje.ProcesarCompra }, cancellationToken: ct);
        await channel.QueueDeclareAsync(DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync(DeadLetterQueue, DeadLetterExchange, TiposMensaje.ProcesarCompra, cancellationToken: ct);
    }
}
