using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Api.Tests;

public sealed class ApiFactory(string environment = "Development", IReadOnlyDictionary<string, string?>? settings = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Promotora"] = null,
                ["Bootstrap:AdminEmail"] = null,
                ["Bootstrap:AdminPassword"] = null
            };
            if (settings is not null)
            {
                foreach (var (key, value) in settings)
                {
                    values[key] = value;
                }
            }

            configuration.AddInMemoryCollection(values);
        });
        // Program registra servicios antes del callback de configuración en minimal hosting.
        // Esta fábrica sin BD nunca debe heredar la conexión local de user-secrets.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<PromotoraDbContext>();
            services.AddScoped(_ => new PromotoraDbContext(new DbContextOptionsBuilder<PromotoraDbContext>()
                .UsePromotoraPostgres("Host=127.0.0.1;Port=1;Database=unconfigured;Username=unconfigured;Timeout=1;Command Timeout=1").Options));
        });
    }
}
