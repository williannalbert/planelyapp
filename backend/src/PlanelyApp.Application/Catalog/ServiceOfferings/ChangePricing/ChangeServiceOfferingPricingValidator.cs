using FluentValidation;

namespace PlanelyApp.Application.Catalog.ServiceOfferings.ChangePricing;

internal sealed class ChangeServiceOfferingPricingValidator : AbstractValidator<ChangeServiceOfferingPricingCommand>
{
    public ChangeServiceOfferingPricingValidator()
    {
        RuleFor(command => command.ServiceOfferingId).NotEmpty();
        RuleFor(command => command.Pricing).NotNull().SetValidator(new PricingDataValidator());
    }
}