namespace Promotora.Application.Shared.Ports;

public interface IClock
{
    DateTime UtcNow { get; }
}
