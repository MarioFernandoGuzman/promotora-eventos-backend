using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Ticketing;

namespace Promotora.Worker.Ticketing;

public sealed record RecuperacionWorkerOptions(int BatchSize = 100, int PollIntervalSeconds = 30, int TimeoutMinutos = 5)
{
    public void Validar()
    {
        if (BatchSize is < 1 or > 1000 || PollIntervalSeconds is < 1 or > 300 || TimeoutMinutos is < 1 or > 1440)
            throw new ArgumentOutOfRangeException(nameof(BatchSize), "Lote, intervalo o timeout de recuperación inválidos.");
    }
}

public sealed class RecuperacionComprasService(IServiceScopeFactory scopes, RecuperacionWorkerOptions options,
    ILogger<RecuperacionComprasService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        options.Validar();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<RecuperarComprasCommand, RecuperacionLoteDto>>();
                var resultado = await handler.HandleAsync(new(options.BatchSize, options.TimeoutMinutos), stoppingToken);
                if (resultado.IsFailure) logger.LogWarning("Recuperación rechazó el lote; se revisará en el siguiente ciclo.");
                else if (resultado.Value.Revisadas > 0)
                    logger.LogInformation("Recuperación: {Completadas} completadas, {Fallidas} fallidas, {Omitidas} omitidas", resultado.Value.Completadas, resultado.Value.Fallidas, resultado.Value.Omitidas);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Recuperación no pudo consultar o finalizar; se reintentará sin devolver cupo por un resultado desconocido."); }
            try { await Task.Delay(TimeSpan.FromSeconds(options.PollIntervalSeconds), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}

public static class RecuperacionWorkerRegistration
{
    public static IServiceCollection AddRecuperacionWorker(this IServiceCollection services, RecuperacionWorkerOptions options)
    {
        options.Validar(); services.AddSingleton(options); services.AddHostedService<RecuperacionComprasService>();
        return services;
    }
}
