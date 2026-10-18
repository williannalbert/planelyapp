using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.SharedKernel;

/// <summary>Phone number in E.164 format: "+" followed by 8 to 15 digits, the first one not zero.</summary>
public sealed class Phone : ValueObject
{
    private Phone(string value) => Value = value;

    public string Value { get; }

    public static Phone Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Phone.Invalid");
        }

        var normalized = new string(value
            .Where(c => !char.IsWhiteSpace(c) && c is not ('-' or '(' or ')' or '.'))
            .ToArray());

        if (!IsE164(normalized))
        {
            throw new DomainException("Phone.Invalid");
        }

        return new Phone(normalized);
    }

    private static bool IsE164(string value) =>
        value.Length is >= 9 and <= 16
        && value[0] == '+'
        && value[1] is >= '1' and <= '9'
        && value.AsSpan(1).IndexOfAnyExceptInRange('0', '9') < 0;

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}