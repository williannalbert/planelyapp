using PlanelyApp.Api.Responses;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Api.Errors;

/// <summary>
/// The only place where a <see cref="Result{TValue}"/> becomes HTTP. An endpoint is one line:
/// <c>(await dispatcher.SendAsync(command, ct)).ToCreated(id => $"/things/{id}")</c>.
/// </summary>
internal static class ResultHttpExtensions
{
    /// <summary>A 200 with the value, or the shape given by <paramref name="onSuccess"/>.</summary>
    public static IResult ToHttp<T>(this Result<T> result, Func<T, IResult>? onSuccess = null) =>
        result.IsFailure
            ? new ErrorResult(result.Error)
            : onSuccess is null ? Results.Ok(result.Value) : onSuccess(result.Value);

    /// <summary>A 204 for a command that returns nothing.</summary>
    public static IResult ToNoContent(this Result<Unit> result) =>
        result.IsFailure ? new ErrorResult(result.Error) : Results.NoContent();

    /// <summary>A 201 with the Location of the new resource and its id in the body.</summary>
    public static IResult ToCreated<T>(this Result<T> result, Func<T, string> location) =>
        result.IsFailure
            ? new ErrorResult(result.Error)
            : Results.Created(location(result.Value), new CreatedResponse<T>(result.Value));
}