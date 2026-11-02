using FluentValidation;

namespace PlanelyApp.Application.Catalog.BranchOfferings.SetAvailability;

internal sealed class SetBranchOfferingAvailabilityValidator : AbstractValidator<SetBranchOfferingAvailabilityCommand>
{
    public SetBranchOfferingAvailabilityValidator()
    {
        RuleFor(command => command.BranchId).NotEmpty();
        RuleFor(command => command.ServiceOfferingId).NotEmpty();
    }
}