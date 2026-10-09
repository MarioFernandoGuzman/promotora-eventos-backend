using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Promotora.Application.Identity.Sessions;
using Promotora.Application.Shared.Handlers;

namespace Promotora.WebApi.Identity;

public static class JwtAuthentication
{
    public static IServiceCollection AddPromotoraAuthentication(this IServiceCollection services, byte[]? key, string issuer, string audience)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.IncludeErrorDetails = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key is null ? null : new SymmetricSecurityKey(key),
                RequireSignedTokens = true,
                RequireExpirationTime = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                NameClaimType = "sub",
                RoleClaimType = "role"
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var principal = context.Principal!;
                    if (!int.TryParse(principal.FindFirstValue("sub"), out int usuarioId)
                        || !long.TryParse(principal.FindFirstValue("sid"), out long sessionId)
                        || !bool.TryParse(principal.FindFirstValue("password_change_required"), out bool change))
                    {
                        context.Fail("Sesión inválida.");
                        return;
                    }

                    var handler = context.HttpContext.RequestServices.GetService<IQueryHandler<ValidarSesionQuery, bool>>();
                    if (handler is null)
                    {
                        context.Fail("Sesión inválida.");
                        return;
                    }

                    var result = await handler.HandleAsync(new ValidarSesionQuery(usuarioId, sessionId, principal.FindFirstValue("role") ?? "", change), context.HttpContext.RequestAborted);
                    if (result.IsFailure || !result.Value)
                    {
                        context.Fail("Sesión inválida.");
                    }
                }
            };
        });
        services.AddAuthorization();
        return services;
    }
}
