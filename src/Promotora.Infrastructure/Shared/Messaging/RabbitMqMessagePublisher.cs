using System.Globalization;
using Promotora.Application.Shared.Messaging;
using Promotora.Application.Shared.Ports;
using RabbitMQ.Client;

namespace Promotora.Infrastructure.Shared.Messaging;

public sealed class RabbitMqMessagePublisher(RabbitMqOptions options) : IMessagePublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private IConnection? connection;
    private IChannel? channel;

    public async Task PrepararAsync(CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.PublishTimeoutSeconds));
        await gate.WaitAsync(timeout.Token);
        try { await ConectarAsync(timeout.Token); }
        finally { gate.Release(); }
    }

    public async Task PublicarAsync(MensajePublicable mensaje, CancellationToken ct)
    {
        if (mensaje.Id <= 0 || !TiposMensaje.EsValido(mensaje.Tipo) || mensaje.Payload.IsEmpty)
            throw new ArgumentException("Mensaje sin identidad, tipo admitido o payload.", nameof(mensaje));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.PublishTimeoutSeconds));
        await gate.WaitAsync(timeout.Token);
        try
        {
            await ConectarAsync(timeout.Token);
            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = mensaje.Id.ToString(CultureInfo.InvariantCulture),
                Type = mensaje.Tipo
            };
            await channel!.BasicPublishAsync(RabbitMqTopology.Exchange, mensaje.Tipo, mandatory: true,
                basicProperties: properties, body: mensaje.Payload, cancellationToken: timeout.Token);
        }
        finally { gate.Release(); }
    }

    private async Task ConectarAsync(CancellationToken ct)
    {
        if (channel is { IsOpen: true }) return;
        if (channel is not null) await channel.DisposeAsync();
        if (connection is not null) await connection.DisposeAsync();
        options.Validar();
        var factory = new ConnectionFactory
        {
            HostName = options.Host,
            Port = options.Port,
            VirtualHost = options.VirtualHost,
            UserName = options.Username,
            Password = options.Password,
            AutomaticRecoveryEnabled = false,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(options.PublishTimeoutSeconds)
        };
        connection = await factory.CreateConnectionAsync(ct);
        channel = await connection.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
        try { await RabbitMqTopology.DeclararAsync(channel, ct); }
        catch
        {
            await channel.DisposeAsync();
            channel = null;
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (channel is not null) await channel.DisposeAsync();
            if (connection is not null) await connection.DisposeAsync();
        }
        finally { gate.Release(); gate.Dispose(); }
    }
}
