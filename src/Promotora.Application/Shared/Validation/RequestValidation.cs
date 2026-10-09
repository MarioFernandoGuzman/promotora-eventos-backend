using FluentValidation;
using Promotora.Domain.Shared;

namespace Promotora.Application.Shared.Validation;

internal static class RequestValidation
{
    public static async Task<IReadOnlyList<Error>> ValidateAsync<T>(T request, IEnumerable<IValidator<T>> validators, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        var errors = new List<Error>();
        // Secuencial: validadores pueden compartir puertos scoped y DbContext.
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(request, ct);
            errors.AddRange(result.Errors.Select(failure => new Error(
                string.IsNullOrWhiteSpace(failure.PropertyName) ? "validation" : failure.PropertyName,
                failure.ErrorMessage, ErrorType.Validation)));
        }

        return errors.Distinct().ToArray();
    }
}
