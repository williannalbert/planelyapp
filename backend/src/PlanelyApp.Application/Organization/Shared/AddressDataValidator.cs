using FluentValidation;

namespace PlanelyApp.Application.Organization.Shared;

/// <summary>Only the shape: which parts must be there. Sizes, state and postal code formats are checked by <c>Address</c>.</summary>
internal sealed class AddressDataValidator : AbstractValidator<AddressData>
{
    public AddressDataValidator()
    {
        RuleFor(address => address.Street).NotEmpty();
        RuleFor(address => address.Number).NotEmpty();
        RuleFor(address => address.District).NotEmpty();
        RuleFor(address => address.City).NotEmpty();
        RuleFor(address => address.State).NotEmpty();
        RuleFor(address => address.PostalCode).NotEmpty();
    }
}