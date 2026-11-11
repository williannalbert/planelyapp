using System.Globalization;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Imports.Processing;

/// <summary>
/// The reasons saved in an import, for a row that was not imported or for a file that failed as a whole. Like the codes
/// of the errors they carry no text: the front translates them. Form: <c>Code</c> or <c>Code(argument;argument)</c>.
/// </summary>
public static class ImportReasons
{
    public const string FileMissing = "Import.FileMissing";
    public const string FileUnreadable = "Import.FileUnreadable";
    public const string FileChanged = "Import.FileChanged";
    public const string NoRows = "Import.NoRows";
    public const string TooManyRows = "Import.TooManyRows";
    public const string MissingColumn = "Import.MissingColumn";
    public const string ValueRequired = "Import.ValueRequired";
    public const string ValueInvalid = "Import.ValueInvalid";
    public const string RowFailed = "Import.RowFailed";

    public static string Of(string code, params object[] arguments) =>
        arguments.Length == 0 ? code : $"{code}({string.Join(';', arguments.Select(Format))})";

    /// <summary>A validation error lists its fields (<c>Field:Code; Field:Code</c>); any other keeps its code and arguments.</summary>
    public static string Of(Error error) => error is ValidationError validation
        ? string.Join("; ", validation.Failures.Select(failure => $"{failure.Field}:{failure.Code}"))
        : Of(error.Code, error.Arguments.ToArray());

    public static string Of(DomainException exception) => Of(exception.Code, exception.Arguments.ToArray());

    private static string Format(object argument) =>
        Convert.ToString(argument, CultureInfo.InvariantCulture) ?? string.Empty;
}