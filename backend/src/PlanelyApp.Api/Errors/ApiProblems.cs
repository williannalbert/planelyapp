using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using PlanelyApp.Api.Correlation;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Api.Errors;

internal sealed record FieldProblem(string Field, string Code);

/// <summary>
/// The one place that builds the body of an error. It carries codes, never texts: the text comes from the code in 5.2.
/// </summary>
internal static class ApiProblems
{
    public const string UnexpectedErrorCode = "Server.UnexpectedError";

    public static ProblemDetails Create(
        HttpContext context,
        int status,
        string code,
        IEnumerable<object>? arguments = null,
        IEnumerable<FieldProblem>? errors = null)
    {
        var problem = new ProblemDetails { Status = status, Title = ReasonPhrases.GetReasonPhrase(status) };
        problem.Extensions["code"] = code;

        var texts = arguments?.Select(Format).ToArray();

        if (texts is { Length: > 0 })
        {
            problem.Extensions["arguments"] = texts;
        }

        if (errors is not null)
        {
            problem.Extensions["errors"] = errors.ToArray();
        }

        Identify(context, problem);

        return problem;
    }

    /// <summary>Also used for the bare statuses (404, 405...) that the status code pages turn into problems.</summary>
    public static void Identify(HttpContext context, ProblemDetails problem)
    {
        if (CorrelationId.Of(context) is { } correlationId)
        {
            problem.Extensions["correlationId"] = correlationId;
        }

        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
    }

    public static int StatusOf(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    };

    public static IResult ToResult(ProblemDetails problem) =>
        Results.Json(problem, statusCode: problem.Status, contentType: "application/problem+json");

    private static string Format(object argument) =>
        Convert.ToString(argument, CultureInfo.InvariantCulture) ?? string.Empty;
}