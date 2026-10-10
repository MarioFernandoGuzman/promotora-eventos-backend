using Promotora.Domain.Shared;

namespace Promotora.Application.Venues.Ports;

public interface IVenuesTransaction
{
    Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> operation, CancellationToken ct);
}
