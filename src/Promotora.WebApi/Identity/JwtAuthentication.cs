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
        services.AddAuthorization(options =>
        {
            options.AddPolicy(IdentityPolicies.Cliente, policy => policy.RequireAuthenticatedUser().RequireRole("CLIENTE").RequireClaim("password_change_required", "false"));
            options.AddPolicy(IdentityPolicies.Administrador, policy => policy.RequireAuthenticatedUser().RequireRole("ADMINISTRADOR").RequireClaim("password_change_required", "false"));
            options.AddPolicy(IdentityPolicies.DuenoRecinto, policy => policy.RequireAuthenticatedUser().RequireRole("DUENO_RECINTO").RequireClaim("password_change_required", "false"));
            options.AddPolicy(IdentityPolicies.SesionCompleta, policy => policy.RequireAuthenticatedUser().RequireClaim("password_change_required", "false"));
            options.AddPolicy(IdentityPolicies.CambioPassword, policy => policy.RequireAuthenticatedUser());
        });
        return services;
    }
}
