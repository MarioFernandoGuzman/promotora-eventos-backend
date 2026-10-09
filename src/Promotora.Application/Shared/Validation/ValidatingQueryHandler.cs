using FluentValidation;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Shared.Validation;

public sealed class ValidatingQueryHandler<TQuery, TResponse>(
    IQueryHandler<TQuery, TResponse> inner, IEnumerable<IValidator<TQuery>> validators)
    : IQueryHandler<TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken ct)
    {
        var errors = await RequestValidation.ValidateAsync(query, validators, ct);
        return errors.Count > 0 ? Result<TResponse>.Failure(errors) : await inner.HandleAsync(query, ct);
    }
}
