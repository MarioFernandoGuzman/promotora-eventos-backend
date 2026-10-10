using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Shared.Messaging;
using Promotora.Application.Ticketing.Ports;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Promotora.Infrastructure.Shared.Messaging;

public sealed class ProcesarCompraConsumer(RabbitMqOptions rabbit, CompraConsumerOptions options, IServiceScopeFactory scopes) : ICompraMessageConsumer
{
    public async Task EjecutarAsync(CancellationToken ct)
    {
        rabbit.Validar(); options.Validar();
        var factory = new ConnectionFactory
        {
            HostName = rabbit.Host,
            Port = rabbit.Port,
            VirtualHost = rabbit.VirtualHost,
            UserName = rabbit.Username,
            Password = rabbit.Password,
            AutomaticRecoveryEnabled = false,
            ConsumerDispatchConcurrency = 1,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(rabbit.PublishTimeoutSeconds)
        };
        await using var connection = await factory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(true, true), ct);
        await RabbitMqTopology.DeclararAsync(channel, ct);
        await channel.BasicQosAsync(0, options.Prefetch, global: false, ct);
        var terminado = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        channel.ChannelShutdownAsync += (_, _) => { terminado.TrySetException(new InvalidOperationException("Canal del consumidor cerrado.")); return Task.CompletedTask; };
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.UnregisteredAsync += (_, _) => { terminado.TrySetException(new InvalidOperationException("Consumidor desregistrado.")); return Task.CompletedTask; };
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            // Copia dentro del callback: RabbitMQ.Client v7 es dueño de la memoria recibida.
            byte[] body = delivery.Body.ToArray();
            try
            {
                bool valido = TryLeer(delivery.BasicProperties, body, out var mensaje, out var intentos);
                bool retry = false;
                bool dlq = !valido || intentos >= options.MaxIntentos;
                if (!dlq)
                {
                    try
                    {
                        await using var scope = scopes.CreateAsyncScope();
                        var result = await scope.ServiceProvider.GetRequiredService<IProcesarCompraMensajePort>().ProcesarAsync(mensaje!, ct);
                        dlq = result.IsFailure;
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                    catch (Exception) { retry = true; }
                }
                if (dlq || retry)
                {
                    int siguiente = intentos + 1;
                    dlq |= siguiente >= options.MaxIntentos;
                    var properties = new BasicProperties(delivery.BasicProperties)
                    {
                        Persistent = true,
                        Headers = delivery.BasicProperties.Headers is null ? [] : new Dictionary<string, object?>(delivery.BasicProperties.Headers)
                    };
                    properties.Headers["x-intentos"] = siguiente;
                    properties.Expiration = dlq ? null : options.Retraso(siguiente).ToString(CultureInfo.InvariantCulture);
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeout.CancelAfter(TimeSpan.FromSeconds(rabbit.PublishTimeoutSeconds));
                    await channel.BasicPublishAsync(dlq ? RabbitMqTopology.DeadLetterExchange : "",
                        dlq ? TiposMensaje.ProcesarCompra : RabbitMqTopology.RetryQueue, true, properties, body, timeout.Token);
                }
                // ACK individual después del commit del puerto o confirms del traslado; nunca auto-ACK.
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception)
            {
                // No ACK si falló el traslado: cerrar canal devuelve el original al broker.
                terminado.TrySetException(new InvalidOperationException("El consumidor no pudo confirmar entrega o traslado."));
            }
        };
        await channel.BasicConsumeAsync(RabbitMqTopology.ComprasQueue, autoAck: false, consumer, ct);
        await terminado.Task.WaitAsync(ct);
    }

    private static bool TryLeer(IReadOnlyBasicProperties properties, byte[] body, out MensajeCompraDto? mensaje, out int intentos)
    {
        mensaje = null; intentos = 0;
        if (properties.Headers?.TryGetValue("x-intentos", out var header) == true)
        {
            if (header is not int count || count is < 0 or > 10) return false;
            intentos = count;
        }
        if (properties.Type != TiposMensaje.ProcesarCompra || properties.ContentType != "application/json"
            || !long.TryParse(properties.MessageId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0
            || body.Length is 0 or > 65536) return false;
        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("publicId", out var value)
                || value.ValueKind != JsonValueKind.String || !value.TryGetGuid(out var publicId) || publicId == Guid.Empty) return false;
            mensaje = new(id, publicId);
            return true;
        }
        catch (JsonException) { return false; }
    }
}
