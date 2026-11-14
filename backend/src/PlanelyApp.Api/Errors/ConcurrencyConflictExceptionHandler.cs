using Microsoft.AspNetCore.Diagnostics;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Api.Errors;

/// <summary>The data changed while the request ran and nothing was saved: a 409 the caller may answer by sending the request again.</summary>
internal sealed class ConcurrencyConflictExceptionHandler(ILogger<ConcurrencyConflictExceptionHandler> logger) : IExceptionHandler
{
    public const string Code = "Concurrency.Conflict";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ConcurrencyConflictException)
        {
            return false;
        }

        // Expected under load, not a failure of the Api: a warning, without the stack trace.
        logger.LogWarning(
            "The data changed while {Method} {Path} ran and nothing was saved.",
            httpContext.Request.Method, httpContext.Request.Path.Value);

        var problem = ApiProblems.Create(httpContext, StatusCodes.Status409Conflict, Code);

        await ApiProblems.ToResult(problem).ExecuteAsync(httpContext);

        return true;
    }
}