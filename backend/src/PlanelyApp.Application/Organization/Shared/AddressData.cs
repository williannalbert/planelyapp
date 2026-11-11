using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Organization.Shared;

/// <summary>An address as it travels in and out of the Api.</summary>
public sealed record AddressData(
    string Street,
    string Number,
    string? Complement,
    string District,
    string City,
    string State,
    string PostalCode)
{
    internal static AddressData From(Address address) => new(
        address.Street,
        address.Number,
        address.Complement,
        address.District,
        address.City,
        address.State,
        address.PostalCode);

    /// <summary>Throws <c>DomainException</c> when a value breaks a limit or a format: those rules are the Domain's.</summary>
    internal Address ToDomain() => Address.Create(Street, Number, Complement, District, City, State, PostalCode);
}