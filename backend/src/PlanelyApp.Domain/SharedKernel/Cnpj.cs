using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.SharedKernel;

/// <summary>
/// Brazilian company id. Supports the numeric and the alphanumeric format (since 2026):
/// 12 characters (digits or A-Z) followed by 2 numeric check digits. Stored without mask.
/// </summary>
public sealed class Cnpj : ValueObject
{
    private static readonly int[] FirstWeights = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] SecondWeights = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    private Cnpj(string value) => Value = value;

    public string Value { get; }

    public string Formatted =>
        $"{Value[..2]}.{Value[2..5]}.{Value[5..8]}/{Value[8..12]}-{Value[12..]}";

    public static Cnpj Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Cnpj.Invalid");
        }

        var normalized = new string(value
            .Where(c => !char.IsWhiteSpace(c) && c is not ('.' or '/' or '-'))
            .ToArray())
            .ToUpperInvariant();

        if (!IsValid(normalized))
        {
            throw new DomainException("Cnpj.Invalid");
        }

        return new Cnpj(normalized);
    }

    private static bool IsValid(string value)
    {
        if (value.Length != 14)
        {
            return false;
        }

        for (var i = 0; i < 12; i++)
        {
            if (!char.IsAsciiDigit(value[i]) && !char.IsAsciiLetterUpper(value[i]))
            {
                return false;
            }
        }

        if (!char.IsAsciiDigit(value[12]) || !char.IsAsciiDigit(value[13]))
        {
            return false;
        }

        if (value.Distinct().Count() == 1)
        {
            return false;
        }

        var first = CheckDigit(value.AsSpan(0, 12), FirstWeights);
        var second = CheckDigit(value.AsSpan(0, 13), SecondWeights);

        return value[12] - '0' == first && value[13] - '0' == second;
    }

    // Character value = ASCII code - 48 ('0' is 48), so digits are 0-9 and letters start at 17.
    private static int CheckDigit(ReadOnlySpan<char> characters, int[] weights)
    {
        var sum = 0;

        for (var i = 0; i < characters.Length; i++)
        {
            sum += (characters[i] - '0') * weights[i];
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