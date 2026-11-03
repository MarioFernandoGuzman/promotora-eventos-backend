using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Identity.Registration;
using Promotora.Infrastructure.Shared;

namespace Promotora.Infrastructure.Identity;

public static class IdentityRegistration
{
    // Los handlers que requieren actor HTTP se registran solo en el composition root de WebApi.
    public static IServiceCollection AddIdentityRegistration(this IServiceCollection services)
    {
        services.AddScoped<IValidator<CrearUsuarioCommand>, CrearUsuarioValidator>();
        services.AddScoped<IValidator<BootstrapAdminCommand>, BootstrapAdminValidator>();
        services.AddCommandHandler<CrearUsuarioCommand, UsuarioDto, CrearUsuarioHandler>();
        services.AddCommandHandler<BootstrapAdminCommand, UsuarioDto, BootstrapAdminHandler>();
        return services;
    }
}
