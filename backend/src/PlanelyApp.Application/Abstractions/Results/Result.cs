namespace PlanelyApp.Application.Abstractions.Results;

/// <summary>
/// The outcome of a use case: a value or an <see cref="Results.Error"/>, never both. It is for the failures the caller
/// is expected to handle. A rule the Domain refuses is still a <c>DomainException</c>.
/// </summary>
public sealed class Result<TValue>
{
    private readonly TValue? _value;
    private readonly Error? _error;

    private Result(TValue value)
    {
        _value = value;
        IsSuccess = true;
    }

    private Result(Error error)
    {
        _error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result has no value. Check IsSuccess first.");

    public Error Error => _error
        ?? throw new InvalidOperationException("A successful result has no error. Check IsFailure first.");

    public static Result<TValue> Success(TValue value) => new(value);

    public static Result<TValue> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<TValue>(error);
    }

    // So a handler can simply "return value;" or "return Error.NotFound(...);".
    public static implicit operator Result<TValue>(TValue value) => Success(value);

    public static implicit operator Result<TValue>(Error error) => Failure(error);
}

/// <summary>Shortcuts for the result of a command that returns nothing.</summary>
public static class Result
{
    public static Result<Unit> Success() => Result<Unit>.Success(Unit.Value);
}