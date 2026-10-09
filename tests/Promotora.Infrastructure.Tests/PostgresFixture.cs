using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Promotora.Infrastructure.Shared.Persistence;
using Testcontainers.PostgreSql;

namespace Promotora.Infrastructure.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly INetwork _network = new NetworkBuilder().Build();
    private readonly PostgreSqlContainer _postgres;
    private IContainer? _flyway;
    public string ConnectionString { get; private set; } = null!;

    public PostgresFixture()
    {
        _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg16")
            .WithDatabase("promotora")
            .WithUsername("postgres")
            .WithPassword(Guid.NewGuid().ToString("N"))
            .WithNetwork(_network)
            .WithNetworkAliases("postgres")
            .Build();
    }

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        await _network.CreateAsync(ct);
        await _postgres.StartAsync(ct);
        var admin = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString());
        _flyway = new ContainerBuilder("flyway/flyway:latest")
            .WithNetwork(_network)
            .WithEnvironment("FLYWAY_URL", "jdbc:postgresql://postgres:5432/promotora")
            .WithEnvironment("FLYWAY_USER", "postgres")
            .WithEnvironment("FLYWAY_PASSWORD", admin.Password!)
            .WithEnvironment("FLYWAY_CONNECT_RETRIES", "20")
            .WithResourceMapping(new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "Migrations")), "/flyway/sql")
            .WithCommand("migrate")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Successfully applied", options => options.WithMode(WaitStrategyMode.OneShot)))
            .Build();
        await _flyway.StartAsync(ct);
        Assert.Equal(0, await _flyway.GetExitCodeAsync(ct));

        await using var connection = new NpgsqlConnection(admin.ConnectionString);
        await connection.OpenAsync(ct);
        // Credencial aleatoria exclusivamente en memoria para esta base temporal.
        var password = Guid.NewGuid().ToString("N");
        await using var command = new NpgsqlCommand($"CREATE ROLE test_app LOGIN PASSWORD '{password}' IN ROLE promotora_app", connection);
        await command.ExecuteNonQueryAsync(ct);
        admin.Username = "test_app";
        admin.Password = password;
        ConnectionString = admin.ConnectionString;
    }

    public PromotoraDbContext CreateContext() => new(
        new DbContextOptionsBuilder<PromotoraDbContext>().UsePromotoraPostgres(ConnectionString).Options);

    public async ValueTask DisposeAsync()
    {
        if (_flyway is not null)
        {
            await _flyway.DisposeAsync();
        }

        await _postgres.DisposeAsync();
        await _network.DisposeAsync();
    }
}
