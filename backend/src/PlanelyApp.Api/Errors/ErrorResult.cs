using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Api.Errors;

/// <summary>An <see cref="Error"/> of a use case as the answer of an endpoint. The body is built when it runs, with the request in hand.</summary>
internal sealed class ErrorResult(Error error) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext)
    {
        var errors = error is ValidationError validation
            ? validation.Failures.Select(failure => new FieldProblem(failure.Field, failure.Code))
            : null;

        var problem = ApiProblems.Create(
            httpContext, ApiProblems.StatusOf(error.Type), error.Code, error.Arguments, errors);

        return ApiProblems.ToResult(problem).ExecuteAsync(httpContext);
    }
}