using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Identity.Ports;

namespace Promotora.Infrastructure.Tests;

public sealed class WorkerRegistrationTests
{
    [Fact]
    public void InfraestructuraDelWorkerNoRequiereActorHttpNiHandlersDelApi()
    {
        var services = new ServiceCollection();
        services.AddPromotoraInfrastructure("Host=localhost;Database=promotora;Username=app");
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUsuarioRepository>());
        Assert.Null(scope.ServiceProvider.GetService<IUsuarioActual>());
    }
}
