using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Api.Errors;

/// <summary>
/// The 400 of a value the framework could bind but that means nothing: a number that is not a weekday or a role, a field the body left
/// out. The field is named as the validators of the Application name it, by the property of the command (<c>nameof</c>).
/// </summary>
internal static class FieldProblems
{
    public static IResult Invalid(string field) => Of(field, "Validation.Invalid");

    public static IResult Required(string field) => Of(field, "Validation.Required");

    private static ErrorResult Of(string field, string code) => new(new ValidationError([new FieldError(field, code)]));
}