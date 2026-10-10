using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Promotora.Application.Shared.Notifications;

namespace Promotora.Worker.Messaging;

public sealed class NotificacionConsumerService(IEnumerable<INotificacionConsumer> consumers, ILogger<NotificacionConsumerService> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.WhenAll(consumers.Select(consumer => EjecutarAsync(consumer, stoppingToken)));

    private async Task EjecutarAsync(INotificacionConsumer consumer, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await consumer.EjecutarAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Consumidor de notificación interrumpido; se reintentará sin confirmar el mensaje."); }
            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
