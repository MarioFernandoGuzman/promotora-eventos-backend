using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Promotora.Infrastructure.Shared.Notifications;

namespace Promotora.Infrastructure.Tests;

public sealed class MailHogFixture : IAsyncLifetime
{
    private readonly IContainer container = new ContainerBuilder("mailhog/mailhog:v1.0.1")
        .WithPortBinding(1025, true).WithPortBinding(8025, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8025).ForPath("/api/v2/messages")))
        .Build();
    public SmtpOptions Options => new(container.Hostname, container.GetMappedPublicPort(1025), "promotora@example.invalid");
    public async ValueTask InitializeAsync() => await container.StartAsync(TestContext.Current.CancellationToken);
    public async ValueTask DisposeAsync() => await container.DisposeAsync();
    public async Task<JsonDocument> MensajesAsync(CancellationToken ct)
    {
        using var client = new HttpClient();
        return JsonDocument.Parse(await client.GetStringAsync($"http://{container.Hostname}:{container.GetMappedPublicPort(8025)}/api/v2/messages?limit=100", ct));
    }
}
