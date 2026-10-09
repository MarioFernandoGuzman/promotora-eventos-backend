using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Shared.Health;

public sealed class DatabaseHealthCheck(IServiceScopeFactory scopes) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetService<PromotoraDbContext>();
        if (db is null)
        {
            return HealthCheckResult.Unhealthy("Conexión no configurada.");
        }

        try
        {
            if (!await db.Database.CanConnectAsync(ct))
            {
                return HealthCheckResult.Unhealthy("Base de datos no disponible.");
            }

            var ready = await db.Database.SqlQuery<bool>($"""
                SELECT NOT r.rolsuper AND NOT r.rolcreatedb AND NOT r.rolcreaterole AND NOT r.rolbypassrls
                    AND pg_has_role(current_user, 'promotora_app', 'MEMBER')
                    AND (SELECT count(*) FROM pg_tables WHERE schemaname='public' AND tablename<>'flyway_schema_history')=26
                    AND (SELECT count(*) FROM pg_views WHERE schemaname='bi')=16
                    AS "Value"
                FROM pg_roles r WHERE r.rolname=current_user
                """).SingleAsync(ct);
            return ready ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Permisos o esquema no preparados.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return HealthCheckResult.Unhealthy("Base de datos no preparada.");
        }
    }
}
