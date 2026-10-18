namespace PlanelyApp.Domain.SeedWork;

public sealed class DomainException : Exception
{
    public DomainException(string code, params object[] arguments) : base(code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
        Arguments = arguments;
    }

    public string Code { get; }

    public IReadOnlyList<object> Arguments { get; }
}