using FluentValidation;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Shared.Validation;

public sealed class ValidatingCommandHandler<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner, IEnumerable<IValidator<TCommand>> validators)
    : ICommandHandler<TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken ct)
    {
        var errors = await RequestValidation.ValidateAsync(command, validators, ct);
        return errors.Count > 0 ? Result<TResponse>.Failure(errors) : await inner.HandleAsync(command, ct);
    }
}
