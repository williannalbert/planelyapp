using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.SharedKernel;

public sealed class Email : ValueObject
{
    private const int MaxLength = 254;

    private Email(string value) => Value = value;

    public string Value { get; }

    public static Email Create(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(normalized) || normalized.Length > MaxLength || !HasValidShape(normalized))
        {
            throw new DomainException("Email.Invalid");
        }

        return new Email(normalized);
    }

    // Intentionally simple: the real proof that an address works is the access code sent to it.
    private static bool HasValidShape(string value)
    {
        var at = value.IndexOf('@');

        if (at <= 0 || at != value.LastIndexOf('@') || at == value.Length - 1)
        {
            return false;
        }

        if (value.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var domain = value[(at + 1)..];

        return domain.Contains('.')
            && !domain.StartsWith('.')
            && !domain.EndsWith('.')
            && !domain.Contains("..", StringComparison.Ordinal);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}