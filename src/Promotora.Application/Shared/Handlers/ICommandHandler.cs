using Promotora.Domain.Shared;

namespace Promotora.Application.Shared.Handlers;

public interface ICommandHandler<in TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken ct);
}
