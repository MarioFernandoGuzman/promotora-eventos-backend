using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Promotora.Application.Ticketing.Ports;

namespace Promotora.Worker.Messaging;

public sealed class ProcesarCompraConsumerService(ICompraMessageConsumer consumer, ILogger<ProcesarCompraConsumerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await consumer.EjecutarAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Consumidor de compras desconectado; se reintentará sin confirmar pendientes"); }
            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
