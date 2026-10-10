using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace Promotora.WebApi.Identity;

public static class IdentityHttpProtection
{
    public const string Login = "identity-login";
    public const string Registro = "identity-registro";
    public const string Compra = "ticketing-compra";
    public const string Cors = "frontend";

    public static IServiceCollection AddIdentityHttpProtection(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            int loginLimit = configuration.GetValue("RateLimiting:LoginPermitLimit", 5);
            int registroLimit = configuration.GetValue("RateLimiting:RegistroPermitLimit", 10);
            int compraLimit = configuration.GetValue("RateLimiting:CompraPermitLimit", 20);
            if (loginLimit is < 1 or > 1000 || registroLimit is < 1 or > 1000 || compraLimit is < 1 or > 1000)
            {
                throw new InvalidOperationException("Los límites HTTP deben estar entre 1 y 1000.");
            }

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Login, context => Partition(context, loginLimit));
            options.AddPolicy(Registro, context => Partition(context, registroLimit));
            options.AddPolicy(Compra, context => Partition(context, compraLimit));
            options.OnRejected = async (context, ct) =>
            {
                double seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry) ? Math.Ceiling(retry.TotalSeconds) : 60;
                context.HttpContext.Response.Headers.RetryAfter = Math.Max(1, seconds).ToString(CultureInfo.InvariantCulture);
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Demasiadas solicitudes",
                    Detail = "Espera antes de volver a intentarlo.",
                    Type = "about:blank"
                };
                problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                await context.HttpContext.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: ct);
            };
        });

        services.AddCors(options =>
        {
            var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            if (origins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https") || uri.AbsolutePath != "/"
                || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || origin.Contains('*') || origin.EndsWith('/')))
            {
                throw new InvalidOperationException("CORS requiere orígenes HTTP/HTTPS explícitos sin comodines, ruta ni barra final.");
            }

            options.AddPolicy(Cors, policy => policy.WithOrigins(origins)
                .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS").AllowAnyHeader().SetPreflightMaxAge(TimeSpan.FromMinutes(10)));
        });
        return services;
    }

    private static RateLimitPartition<string> Partition(HttpContext context, int limit) => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit,
            QueueLimit = 0,
            Window = TimeSpan.FromMinutes(1),
            AutoReplenishment = true
        });
}
