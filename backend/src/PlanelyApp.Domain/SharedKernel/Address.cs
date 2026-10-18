using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.SharedKernel;

public sealed class Address : ValueObject
{
    private Address(
        string street,
        string number,
        string? complement,
        string district,
        string city,
        string state,
        string postalCode)
    {
        Street = street;
        Number = number;
        Complement = complement;
        District = district;
        City = city;
        State = state;
        PostalCode = postalCode;
    }

    public string Street { get; }

    public string Number { get; }

    public string? Complement { get; }

    public string District { get; }

    public string City { get; }

    /// <summary>Two-letter state code (UF), upper case.</summary>
    public string State { get; }

    /// <summary>Brazilian postal code (CEP): 8 digits, no mask.</summary>
    public string PostalCode { get; }

    public static Address Create(
        string? street,
        string? number,
        string? complement,
        string? district,
        string? city,
        string? state,
        string? postalCode)
    {
        return new Address(
            Guard.RequiredText(street, 150, "Address.StreetInvalid"),
            Guard.RequiredText(number, 20, "Address.NumberInvalid"),
            string.IsNullOrWhiteSpace(complement)
                ? null
                : Guard.RequiredText(complement, 100, "Address.ComplementInvalid"),
            Guard.RequiredText(district, 100, "Address.DistrictInvalid"),
            Guard.RequiredText(city, 100, "Address.CityInvalid"),
            NormalizeState(state),
            NormalizePostalCode(postalCode));
    }

    private static string Required(string? value, int maxLength, string errorCode)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > maxLength)
        {
            throw new DomainException(errorCode);
        }

        return trimmed;
    }

    private static string NormalizeState(string? state)
    {
        var normalized = state?.Trim().ToUpperInvariant();

        if (normalized is not { Length: 2 } || !normalized.All(char.IsAsciiLetter))
        {
            throw new DomainException("Address.StateInvalid");
        }

        return normalized;
    }

    private static string NormalizePostalCode(string? postalCode)
    {
        var digits = new string((postalCode ?? string.Empty)
            .Where(c => !char.IsWhiteSpace(c) && c is not ('-' or '.'))
            .ToArray());

        if (digits.Length != 8 || !digits.All(char.IsAsciiDigit))
        {
            throw new DomainException("Address.PostalCodeInvalid");
        }

        return digits;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return Number;
        yield return Complement;
        yield return District;
        yield return City;
        yield return State;
        yield return PostalCode;
    }
}