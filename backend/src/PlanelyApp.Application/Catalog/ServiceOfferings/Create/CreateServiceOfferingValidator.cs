using FluentValidation;

namespace PlanelyApp.Application.Catalog.ServiceOfferings.Create;

internal sealed class CreateServiceOfferingValidator : AbstractValidator<CreateServiceOfferingCommand>
{
    public CreateServiceOfferingValidator()
    {
        // The lengths and the range of the duration are the Domain's.
        RuleFor(command => command.Name).NotEmpty();
        RuleFor(command => command.Pricing).NotNull().SetValidator(new PricingDataValidator());
        RuleFor(command => command.CategoryId).NotEqual(Guid.Empty);
    }
}