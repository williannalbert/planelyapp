using Microsoft.AspNetCore.Diagnostics;

namespace PlanelyApp.Api.Errors;

/// <summary>Anything else is a failure of the Api: a 500 with no detail of the exception in the body. The detail goes to the log.</summary>
internal sealed class UnhandledExceptionHandler(ILogger<UnhandledExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // The caller closed the connection: nobody is there to read an answer, and it is not an error of ours.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        logger.LogError(
            exception, "Unhandled exception on {Method} {Path}.", httpContext.Request.Method, httpContext.Request.Path.Value);

        var problem = ApiProblems.Create(
            httpContext, StatusCodes.Status500InternalServerError, ApiProblems.UnexpectedErrorCode);

        await ApiProblems.ToResult(problem).ExecuteAsync(httpContext);

        return true;
    }
}