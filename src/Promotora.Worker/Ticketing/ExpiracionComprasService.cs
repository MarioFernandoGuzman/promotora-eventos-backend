using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Ticketing;

namespace Promotora.Worker.Ticketing;

public sealed class ExpiracionComprasService(IServiceScopeFactory scopes, ExpiracionWorkerOptions options,
    ILogger<ExpiracionComprasService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        options.Validar();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<ExpirarComprasCommand, ExpiracionLoteDto>>();
                var resultado = await handler.HandleAsync(new(options.BatchSize), stoppingToken);
                if (resultado.IsFailure) logger.LogWarning("Expiración rechazó el lote; se revisará en el siguiente ciclo.");
                else if (resultado.Value.Expiradas > 0) logger.LogInformation("Expiración confirmó {Cantidad} compras vencidas", resultado.Value.Expiradas);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Expiración no pudo completar el lote; se reintentará sin confirmar cambios parciales."); }
            try { await Task.Delay(TimeSpan.FromSeconds(options.PollIntervalSeconds), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
