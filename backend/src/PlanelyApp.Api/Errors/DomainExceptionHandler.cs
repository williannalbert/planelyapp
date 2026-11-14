using Microsoft.AspNetCore.Diagnostics;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Api.Errors;

/// <summary>A rule of the Domain said no: a 422 with the code of the rule.</summary>
internal sealed class DomainExceptionHandler(ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainException domain)
        {
            return false;
        }

        // An expected answer, not a failure of the Api: no stack trace in the log.
        logger.LogInformation("A rule of the Domain refused the request: {Code}.", domain.Code);

        var problem = ApiProblems.Create(
            httpContext, StatusCodes.Status422UnprocessableEntity, domain.Code, domain.Arguments);

        await ApiProblems.ToResult(problem).ExecuteAsync(httpContext);

        return true;
    }
}