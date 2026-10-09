using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Promotora.Infrastructure;
using Promotora.Infrastructure.Shared.Health;

namespace Promotora.Infrastructure.Tests;

public sealed class HealthTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task ReadinessCompruebaEsquemaYPermisosReales()
    {
        var services = new ServiceCollection();
        services.AddPromotoraInfrastructure(fixture.ConnectionString);
        await using var provider = services.BuildServiceProvider();
        var check = new DatabaseHealthCheck(provider.GetRequiredService<IServiceScopeFactory>());
        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}
