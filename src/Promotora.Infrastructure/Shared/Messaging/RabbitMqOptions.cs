namespace Promotora.Infrastructure.Shared.Messaging;

public sealed class RabbitMqOptions
{
    public required string Host { get; init; }
    public int Port { get; init; } = 5672;
    public string VirtualHost { get; init; } = "/";
    public required string Username { get; init; }
    public required string Password { get; init; }
    public int PublishTimeoutSeconds { get; init; } = 15;

    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(Host) || string.IsNullOrWhiteSpace(VirtualHost)
            || string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password)
            || Port is < 1 or > 65535 || PublishTimeoutSeconds is < 1 or > 120)
            throw new InvalidOperationException("RabbitMQ requiere host, puerto, vhost, credenciales externas y timeout válido.");
    }
}
