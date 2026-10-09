using Promotora.Domain.Shared;

namespace Promotora.Application.Shared.Handlers;

public interface IQueryHandler<in TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken ct);
}
