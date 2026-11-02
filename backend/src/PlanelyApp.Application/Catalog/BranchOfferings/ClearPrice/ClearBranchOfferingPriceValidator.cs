using FluentValidation;

namespace PlanelyApp.Application.Catalog.BranchOfferings.ClearPrice;

internal sealed class ClearBranchOfferingPriceValidator : AbstractValidator<ClearBranchOfferingPriceCommand>
{
    public ClearBranchOfferingPriceValidator()
    {
        RuleFor(command => command.BranchId).NotEmpty();
        RuleFor(command => command.ServiceOfferingId).NotEmpty();
    }
}