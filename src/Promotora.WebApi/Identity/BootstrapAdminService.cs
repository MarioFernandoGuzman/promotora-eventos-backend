using Promotora.Application.Identity.Registration;
using Promotora.Application.Shared.Handlers;

namespace Promotora.WebApi.Identity;

public sealed class BootstrapAdminService(IServiceScopeFactory scopes, IConfiguration configuration) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var email = configuration["Bootstrap:AdminEmail"];
        var password = configuration["Bootstrap:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Promotora"))
            || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Bootstrap requiere conexión, correo y contraseña externos.");
        }

        await using var scope = scopes.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<BootstrapAdminCommand, UsuarioDto>>();
        var result = await handler.HandleAsync(new BootstrapAdminCommand(email, password), ct);
        if (result.IsFailure)
        {
            throw new InvalidOperationException("No se pudo inicializar el administrador. Comprueba la configuración del bootstrap.");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
