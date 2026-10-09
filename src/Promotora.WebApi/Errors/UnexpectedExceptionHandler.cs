using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Promotora.WebApi.Errors;

public sealed class UnexpectedExceptionHandler(IProblemDetailsService problems, ILogger<UnexpectedExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        logger.LogError("Error inesperado de tipo {ExceptionType}; CorrelationId {CorrelationId}", exception.GetType().Name, context.TraceIdentifier);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Error interno",
            Type = "about:blank",
            Detail = "Ocurrió un error inesperado. Usa el identificador de correlación para reportarlo."
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;
        if (!await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem }))
        {
            await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: ct);
        }

        return true;
    }
}
