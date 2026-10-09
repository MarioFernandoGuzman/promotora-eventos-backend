using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Ports;
using Promotora.Infrastructure.Audit;
using Promotora.Infrastructure.Identity;
using Promotora.Infrastructure.Identity.Persistence;
using Promotora.Infrastructure.Shared;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPromotoraInfrastructure(this IServiceCollection services, string? connectionString)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordService, IdentityPasswordService>();
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<PromotoraDbContext>(options => options.UsePromotoraPostgres(connectionString));
            services.AddScoped<IUnitOfWork, EfUnitOfWork>();
            services.AddScoped<IAuditLogger, EfAuditLogger>();
            services.AddScoped<IUsuarioRepository, EfUsuarioRepository>();
        }

        return services;
    }
}
