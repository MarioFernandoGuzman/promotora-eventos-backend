using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
            .WithPortBinding(15672, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Server startup complete"))
            .Build();
    }

    public async ValueTask InitializeAsync() => await container.StartAsync(TestContext.Current.CancellationToken);
    public async ValueTask DisposeAsync() => await container.DisposeAsync();
    public async Task<(int Listos, int SinAck)> EstadoColaAsync(string queue, CancellationToken ct)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"test_app:{password}")));
        using var response = await client.GetAsync($"http://{container.Hostname}:{container.GetMappedPublicPort(15672)}/api/queues/%2F/{Uri.EscapeDataString(queue)}", ct);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        // Management aún puede no tener el primer muestreo: desconocido no equivale a cero.
        return json.RootElement.TryGetProperty("messages_ready", out var ready)
            && json.RootElement.TryGetProperty("messages_unacknowledged", out var pending)
            ? (ready.GetInt32(), pending.GetInt32()) : (-1, -1);
    }
    public Task<IConnection> ConectarAsync(CancellationToken ct) => new ConnectionFactory
    {
        HostName = Options.Host,
        Port = Options.Port,
        UserName = Options.Username,
        Password = Options.Password,
        AutomaticRecoveryEnabled = false
    }.CreateConnectionAsync(ct);

    // Solo el broker efímero de esta fixture; interrumpe una conexión, no elimina colas ni mensajes.
    public async Task InterrumpirConsumidorAsync(string queue, CancellationToken ct)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"test_app:{password}")));
        string root = $"http://{container.Hostname}:{container.GetMappedPublicPort(15672)}/api";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(30));
        string? connection = null;
        while (connection is null)
        {
            using var response = await client.GetAsync($"{root}/queues/%2F/{Uri.EscapeDataString(queue)}", timeout.Token);
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            var consumers = json.RootElement.GetProperty("consumer_details");
            if (consumers.GetArrayLength() > 0)
                connection = Assert.Single(consumers.EnumerateArray()).GetProperty("channel_details").GetProperty("connection_name").GetString();
            else await Task.Delay(100, timeout.Token);
        }
        using var closed = await client.DeleteAsync($"{root}/connections/{Uri.EscapeDataString(connection)}", ct);
        closed.EnsureSuccessStatusCode();
    }
}
