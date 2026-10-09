using Microsoft.AspNetCore.Mvc;
using Promotora.Domain.Shared;

namespace Promotora.WebApi.Errors;

public static class ProblemDetailsMapper
{
    public static ProblemDetails Create(IReadOnlyList<Error> errors, HttpContext context)
    {
        if (errors.Count == 0)
        {
            throw new ArgumentException("Se requiere al menos un error.", nameof(errors));
        }

        var first = errors.FirstOrDefault(e => e.Type != ErrorType.Validation) ?? errors[0];
        var status = first.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
            ErrorType.RateLimit => StatusCodes.Status429TooManyRequests,
            _ => throw new ArgumentOutOfRangeException(nameof(errors))
        };
        ProblemDetails problem = first.Type == ErrorType.Validation
            ? new ValidationProblemDetails(errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray()))
            : new ProblemDetails();
        problem.Status = status;
        problem.Title = Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(status);
        problem.Type = "about:blank";
        problem.Detail = first.Message;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        problem.Extensions["codes"] = errors.Select(e => e.Code).Distinct().ToArray();
        return problem;
    }
}
