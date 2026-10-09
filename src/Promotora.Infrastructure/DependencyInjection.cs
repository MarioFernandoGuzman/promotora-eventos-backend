using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Shared.Ports;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPromotoraInfrastructure(this IServiceCollection services, string? connectionString)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, SystemClock>();
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<PromotoraDbContext>(options => options.UsePromotoraPostgres(connectionString));
            services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        }

        return services;
    }
}
