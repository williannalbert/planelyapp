namespace PlanelyApp.Application.Abstractions.Results;

/// <summary>
/// Why an operation did not work, in a way the caller can act on. Like a domain error it carries no text:
/// the Api translates <see cref="Code"/> to the language of the request, and <see cref="Arguments"/> fill its placeholders.
/// </summary>
public record Error
{
    public Error(string code, ErrorType type, params object[] arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(arguments);

        Code = code;
        Type = type;
        Arguments = arguments;
    }

    /// <summary>Stable, in the same form as the domain codes: "Appointment.NotFound".</summary>
    public string Code { get; }

    public ErrorType Type { get; }

    public IReadOnlyList<object> Arguments { get; }

    public static Error Validation(string code, params object[] arguments) => new(code, ErrorType.Validation, arguments);

    public static Error NotFound(string code, params object[] arguments) => new(code, ErrorType.NotFound, arguments);

    public static Error Conflict(string code, params object[] arguments) => new(code, ErrorType.Conflict, arguments);

    public static Error Forbidden(string code, params object[] arguments) => new(code, ErrorType.Forbidden, arguments);
}