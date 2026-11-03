using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Promotora.Application.Shared.Ports;

namespace Promotora.Worker.Messaging;

public sealed class OutboxRelayService(IServiceScopeFactory scopes, IMessagePublisher publisher,
    OutboxRelayOptions options, ILogger<OutboxRelayService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await publisher.PrepararAsync(stoppingToken);
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<IOutboxRelay>().PublicarLoteAsync(options.BatchSize, stoppingToken);
                if (result.Publicados > 0) logger.LogInformation("Outbox confirmó {Cantidad} mensajes", result.Publicados);
                if (result.Fallidos > 0) logger.LogWarning("Outbox conserva un mensaje pendiente; se reintentará");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception)
            {
                logger.LogWarning("Outbox no pudo completar el ciclo; se reintentará sin perder pendientes");
            }
            try { await Task.Delay(TimeSpan.FromSeconds(options.PollIntervalSeconds), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
