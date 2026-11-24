using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using PlanelyApp.Api.Correlation;
using PlanelyApp.Api.Resources;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Api.Errors;

/// <summary>A field refused. <c>Message</c> is the text of <c>Code</c> in the language of the request, when there is one.</summary>
internal sealed record FieldProblem(
    string Field,
    string Code,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Message = null);

/// <summary>
/// The one place that builds the body of an error. The code is the contract; <c>detail</c> is the text of the code
/// in the language of the request. The text of an exception never gets here.
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
        var texts = context.RequestServices.GetRequiredService<IErrorTexts>();
        context.Response.Headers.ContentLanguage = CultureInfo.CurrentUICulture.Name;

        var problem = new ProblemDetails { Status = status, Title = ReasonPhrases.GetReasonPhrase(status) };
        problem.Extensions["code"] = code;

        var formatted = arguments?.Select(Format).ToArray();

        problem.Detail = texts.Find(code, formatted);

        if (formatted is { Length: > 0 })
        {
            problem.Extensions["arguments"] = formatted;
        }

        if (errors is not null)
        {
            problem.Extensions["errors"] = errors
                .Select(error => error with { Message = texts.Find(error.Code) })
                .ToArray();
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