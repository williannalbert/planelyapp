using FluentValidation;

namespace PlanelyApp.Application.Catalog.BranchOfferings.SetPrice;

internal sealed class SetBranchOfferingPriceValidator : AbstractValidator<SetBranchOfferingPriceCommand>
{
    public SetBranchOfferingPriceValidator()
    {
        RuleFor(command => command.BranchId).NotEmpty();
        RuleFor(command => command.ServiceOfferingId).NotEmpty();

        // The column is decimal(12,2): more than two decimals would be rounded without notice, more than ten digits
        // would not fit. A negative price is the Domain's (Money.NegativeAmount).
        RuleFor(command => command.Price).PrecisionScale(12, 2, true);
    }
}