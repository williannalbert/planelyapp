using FluentValidation;

namespace PlanelyApp.Application.Catalog.ServiceOfferings.UpdateDetails;

internal sealed class UpdateServiceOfferingDetailsValidator : AbstractValidator<UpdateServiceOfferingDetailsCommand>
{
    public UpdateServiceOfferingDetailsValidator()
    {
        RuleFor(command => command.ServiceOfferingId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty();
        RuleFor(command => command.CategoryId).NotEqual(Guid.Empty);
    }
}