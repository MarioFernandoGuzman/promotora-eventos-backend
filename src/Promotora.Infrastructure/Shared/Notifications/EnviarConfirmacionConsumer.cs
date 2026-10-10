using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Shared.Messaging;
using Promotora.Application.Shared.Notifications;
using Promotora.Infrastructure.Shared.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Promotora.Infrastructure.Shared.Notifications;

public sealed class EnviarConfirmacionConsumer(RabbitMqOptions rabbit, IServiceScopeFactory scopes) : INotificacionConsumer
{
    public async Task EjecutarAsync(CancellationToken ct)
    {
        rabbit.Validar();
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
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
        await RabbitMqTopology.DeclararAsync(channel, ct);
        await channel.BasicQosAsync(0, 1, false, ct);
        var terminado = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        channel.ChannelShutdownAsync += (_, _) => { terminado.TrySetException(new InvalidOperationException("Canal de notificación cerrado.")); return Task.CompletedTask; };
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.UnregisteredAsync += (_, _) => { terminado.TrySetException(new InvalidOperationException("Consumidor de notificación desregistrado.")); return Task.CompletedTask; };
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            var body = delivery.Body.ToArray();
            try
            {
                if (!long.TryParse(delivery.BasicProperties.MessageId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
                    throw new InvalidOperationException("Notificación sin identidad deduplicable.");
                bool valido = delivery.BasicProperties.Type == TiposMensaje.CompraCompletada
                    && delivery.BasicProperties.ContentType == "application/json" && body.Length is > 0 and <= 65536;
                Guid? publicId = null;
                if (valido)
                {
                    try
                    {
                        using var json = JsonDocument.Parse(body);
                        valido = json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("publicId", out var value)
                            && value.ValueKind == JsonValueKind.String && value.TryGetGuid(out var guid) && guid != Guid.Empty;
                        if (valido) publicId = json.RootElement.GetProperty("publicId").GetGuid();
                    }
                    catch (JsonException) { valido = false; }
                }
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<INotificacionMensajePort>().ProcesarAsync(new(id, publicId, valido), ct);
                await channel.BasicAckAsync(delivery.DeliveryTag, false, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception) { terminado.TrySetException(new InvalidOperationException("Notificación pendiente de confirmación.")); }
        };
        await channel.BasicConsumeAsync("notificaciones.compra", false, consumer, ct);
        await terminado.Task.WaitAsync(ct);
    }
}
