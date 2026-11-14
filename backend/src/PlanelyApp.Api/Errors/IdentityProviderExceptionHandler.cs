using Microsoft.AspNetCore.Diagnostics;
using PlanelyApp.Application.Abstractions.Identity;

namespace PlanelyApp.Api.Errors;

/// <summary>Keycloak could not be reached, was too slow or refused in a way nobody can act on: a 503. Its message stays in the log, never in the body.</summary>
internal sealed class IdentityProviderExceptionHandler(ILogger<IdentityProviderExceptionHandler> logger) : IExceptionHandler
{
    public const string Code = "Identity.Unavailable";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not IdentityProviderException)
        {
            return false;
        }

        logger.LogWarning(
            exception, "The identity provider failed on {Method} {Path}.",
            httpContext.Request.Method, httpContext.Request.Path.Value);

        var problem = ApiProblems.Create(httpContext, StatusCodes.Status503ServiceUnavailable, Code);

        await ApiProblems.ToResult(problem).ExecuteAsync(httpContext);

        return true;
    }
}