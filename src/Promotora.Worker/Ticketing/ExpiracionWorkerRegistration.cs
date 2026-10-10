using Microsoft.Extensions.DependencyInjection;

namespace Promotora.Worker.Ticketing;

public static class ExpiracionWorkerRegistration
{
    public static IServiceCollection AddExpiracionWorker(this IServiceCollection services, ExpiracionWorkerOptions options)
    {
        options.Validar();
        services.AddSingleton(options);
        services.AddHostedService<ExpiracionComprasService>();
        return services;
    }
}
