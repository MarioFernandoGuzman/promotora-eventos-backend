using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Identity.Sessions;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Shared;
using Promotora.Infrastructure.Identity.Persistence;
using Promotora.Infrastructure.Shared;

namespace Promotora.Infrastructure.Identity;

public static class IdentitySessionRegistration
{
    public static IServiceCollection AddIdentitySessions(this IServiceCollection services, byte[] key, string issuer, string audience, SesionOptions options)
    {
        if (key.Length < 32 || options.AccessMinutes is < 1 or > 30 || options.RefreshDays is < 1 or > 30)
        {
            throw new ArgumentException("Configuración de sesión inválida.");
        }

        services.AddSingleton(options);
        services.AddSingleton<ITokenService>(provider => new JwtTokenService(key, issuer, audience, provider.GetRequiredService<IClock>()));
        services.AddScoped<ISesionRepository, EfSesionRepository>();
        services.AddScoped<IValidator<LoginCommand>, LoginValidator>();
        services.AddScoped<IValidator<RefreshCommand>, RefreshValidator>();
        services.AddScoped<IValidator<LogoutCommand>, LogoutValidator>();
        services.AddCommandHandler<LoginCommand, TokenDto, LoginHandler>();
        services.AddCommandHandler<RefreshCommand, TokenDto, RefreshHandler>();
        services.AddCommandHandler<LogoutCommand, Unit, LogoutHandler>();
        services.AddQueryHandler<ValidarSesionQuery, bool, ValidarSesionHandler>();
        return services;
    }
}
