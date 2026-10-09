using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace Promotora.Infrastructure.Shared.Persistence;

public static class PersistenceOptions
{
    public static DbContextOptionsBuilder<PromotoraDbContext> UsePromotoraPostgres(
        this DbContextOptionsBuilder<PromotoraDbContext> builder, string connectionString)
    {
        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        if (connection.IncludeErrorDetail || connection.LogParameters)
        {
            throw new ArgumentException("La conexión no debe registrar parámetros ni detalles sensibles.", nameof(connectionString));
        }

        return builder.UseNpgsql(connectionString, postgres => postgres.UseVector())
            .UseSnakeCaseNamingConvention();
    }
}
