using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Promotora.Infrastructure.Shared.Messaging;
using RabbitMQ.Client;

namespace Promotora.Infrastructure.Tests;

public sealed class RabbitMqFixture : IAsyncLifetime
{
    private readonly string password = Guid.NewGuid().ToString("N");
    private readonly IContainer container;
    public RabbitMqOptions Options => new()
    {
        Host = container.Hostname,
        Port = container.GetMappedPublicPort(5672),
        Username = "test_app",
        Password = password
    };

    public RabbitMqFixture()
    {
        container = new ContainerBuilder("rabbitmq:4-management")
            .WithEnvironment("RABBITMQ_DEFAULT_USER", "test_app")
            .WithEnvironment("RABBITMQ_DEFAULT_PASS", password)
            .WithPortBinding(5672, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Server startup complete"))
            .Build();
    }

    public async ValueTask InitializeAsync() => await container.StartAsync(TestContext.Current.CancellationToken);
    public async ValueTask DisposeAsync() => await container.DisposeAsync();
    public Task<IConnection> ConectarAsync(CancellationToken ct) => new ConnectionFactory
    {
        HostName = Options.Host,
        Port = Options.Port,
        UserName = Options.Username,
        Password = Options.Password,
        AutomaticRecoveryEnabled = false
    }.CreateConnectionAsync(ct);
}
