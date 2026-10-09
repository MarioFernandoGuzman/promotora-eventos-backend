using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

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
    }
}
