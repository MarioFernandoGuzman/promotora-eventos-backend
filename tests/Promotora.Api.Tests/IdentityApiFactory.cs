using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Promotora.Application.Identity.Sessions;
using Promotora.Application.Ticketing;
using Promotora.Infrastructure;
using Promotora.Infrastructure.Catalog;
using Promotora.Infrastructure.Identity;
using Promotora.Infrastructure.Images;
using Promotora.Infrastructure.Ticketing;
using Promotora.Infrastructure.Venues;

namespace Promotora.Api.Tests;

public sealed class IdentityApiFactory(string connection, string? adminEmail = null, string? adminPassword = null) : WebApplicationFactory<Program>
{
    public byte[] Key { get; } = RandomNumberGenerator.GetBytes(64);
    public string ImagesRoot { get; } = Path.Combine(Path.GetTempPath(), "promotora-images-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Promotora"] = connection,
            ["Bootstrap:AdminEmail"] = adminEmail,
            ["Bootstrap:AdminPassword"] = adminPassword,
            ["Jwt:SigningKey"] = Convert.ToBase64String(Key),
            ["RateLimiting:LoginPermitLimit"] = "1000",
            ["RateLimiting:RegistroPermitLimit"] = "1000",
            ["RateLimiting:CompraPermitLimit"] = "1000"
        }));
        // Minimal hosting ejecuta Program antes del callback de configuración del host de pruebas.
        // Reemplaza sus adaptadores con los mismos registros de producción, apuntando a PostgreSQL temporal.
        builder.ConfigureTestServices(services =>
        {
            services.AddPromotoraInfrastructure(connection);
            services.AddIdentityRegistration();
            services.AddIdentitySessions(Key, "promotora", "promotora-api", new SesionOptions());
            services.AddIdentityManagement();
            services.AddVenues();
            services.AddCatalog();
            services.AddTicketing(new CompraOptions());
            services.AddImages(ImagesRoot);
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Key);
                options.TokenValidationParameters.ValidIssuer = "promotora";
                options.TokenValidationParameters.ValidAudience = "promotora-api";
            });
        });
    }
}
