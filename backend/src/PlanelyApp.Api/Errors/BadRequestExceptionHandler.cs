using Microsoft.AspNetCore.Diagnostics;

namespace PlanelyApp.Api.Errors;

/// <summary>
/// In Development the framework throws when it cannot read the body; in Production it only sets the status, and the status
/// code pages turn that into a problem. This makes the first behave like the second. The text of the exception names types
/// and properties, so it never goes into the body.
/// </summary>
internal sealed class BadRequestExceptionHandler(ILogger<BadRequestExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException bad)
        {
            return false;
        }

        // The Api has texts for these two. Anything else the framework reports about a body is, for the client, a bad request.
        var status = bad.StatusCode == StatusCodes.Status415UnsupportedMediaType
            ? StatusCodes.Status415UnsupportedMediaType
            : StatusCodes.Status400BadRequest;

        logger.LogInformation("The request could not be read before reaching the endpoint: {Status}.", status);

        var problem = ApiProblems.Create(httpContext, status, ApiProblems.HttpCodeOf(status));

        await ApiProblems.ToResult(problem).ExecuteAsync(httpContext);

        return true;
    }
}