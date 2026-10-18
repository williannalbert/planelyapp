using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.SharedKernel;

/// <summary>Brazilian individual taxpayer id: 11 digits, the last 2 are check digits. Stored without mask.</summary>
public sealed class Cpf : ValueObject
{
    private Cpf(string value) => Value = value;

    public string Value { get; }

    public string Formatted => $"{Value[..3]}.{Value[3..6]}.{Value[6..9]}-{Value[9..]}";

    public static Cpf Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Cpf.Invalid");
        }

        var normalized = new string(value
            .Where(c => !char.IsWhiteSpace(c) && c is not ('.' or '-'))
            .ToArray());

        if (!IsValid(normalized))
        {
            throw new DomainException("Cpf.Invalid");
        }

        return new Cpf(normalized);
    }

    private static bool IsValid(string value)
    {
        if (value.Length != 11 || !value.All(char.IsAsciiDigit) || value.Distinct().Count() == 1)
        {
            return false;
        }

        var first = CheckDigit(value.AsSpan(0, 9), 10);
        var second = CheckDigit(value.AsSpan(0, 10), 11);

        return value[9] - '0' == first && value[10] - '0' == second;
    }

    private static int CheckDigit(ReadOnlySpan<char> digits, int firstWeight)
    {
        var sum = 0;

        for (var i = 0; i < digits.Length; i++)
        {
            sum += (digits[i] - '0') * (firstWeight - i);
        }

        var remainder = sum % 11;

        return remainder < 2 ? 0 : 11 - remainder;
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}