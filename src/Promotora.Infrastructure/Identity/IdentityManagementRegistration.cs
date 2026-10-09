using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Identity.Management;
using Promotora.Domain.Shared;
using Promotora.Infrastructure.Shared;

namespace Promotora.Infrastructure.Identity;

public static class IdentityManagementRegistration
{
    public static IServiceCollection AddIdentityManagement(this IServiceCollection services)
    {
        services.AddScoped<UsuarioAccess>();
        services.AddScoped<IValidator<ListarUsuariosQuery>, ListarUsuariosValidator>();
        services.AddScoped<IValidator<ActualizarUsuarioCommand>, ActualizarUsuarioValidator>();
        services.AddScoped<IValidator<CambiarPasswordCommand>, CambiarPasswordValidator>();
        services.AddQueryHandler<ObtenerUsuarioQuery, UsuarioDetalleDto, ObtenerUsuarioHandler>();
        services.AddQueryHandler<ListarUsuariosQuery, UsuariosPaginaDto, ListarUsuariosHandler>();
        services.AddCommandHandler<ActualizarUsuarioCommand, UsuarioDetalleDto, ActualizarUsuarioHandler>();
        services.AddCommandHandler<BajaUsuarioCommand, Unit, BajaUsuarioHandler>();
        services.AddCommandHandler<CambiarPasswordCommand, Unit, CambiarPasswordHandler>();
        return services;
    }
}
