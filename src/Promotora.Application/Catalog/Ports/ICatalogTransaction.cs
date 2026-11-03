using Promotora.Domain.Shared;

namespace Promotora.Application.Catalog.Ports;

public interface ICatalogTransaction
{
    Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> operation, CancellationToken ct);
}
