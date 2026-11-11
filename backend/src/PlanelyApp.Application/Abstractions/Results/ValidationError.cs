namespace PlanelyApp.Application.Abstractions.Results;

/// <summary>The data of the request is malformed. It says which fields, so the client can mark them (400).</summary>
public sealed record ValidationError : Error
{
    public const string FailedCode = "Validation.Failed";

    public ValidationError(IReadOnlyList<FieldError> failures) : base(FailedCode, ErrorType.Validation)
    {
        ArgumentNullException.ThrowIfNull(failures);
        Failures = failures;
    }

    public IReadOnlyList<FieldError> Failures { get; }
}