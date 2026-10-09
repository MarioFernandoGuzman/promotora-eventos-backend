using Promotora.Application.Shared.Ports;

namespace Promotora.Infrastructure.Shared;

public sealed class SystemClock(TimeProvider timeProvider) : IClock
{
    public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;
}
