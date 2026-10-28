namespace PlanelyApp.Application.Abstractions.Validation;

/// <summary>The codes of a field problem. A validator can also set its own code with <c>WithErrorCode</c>.</summary>
public static class ValidationCodes
{
    public const string Required = "Validation.Required";

    public const string Invalid = "Validation.Invalid";

    public const string TooLong = "Validation.TooLong";

    public const string OutOfRange = "Validation.OutOfRange";
}