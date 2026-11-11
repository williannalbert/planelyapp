using FluentValidation;

namespace PlanelyApp.Application.Catalog.Promotions.ChangeValidity;

internal sealed class ChangePromotionValidityValidator : AbstractValidator<ChangePromotionValidityCommand>
{
    public ChangePromotionValidityValidator()
    {
        // That the period does not end before it starts is the Domain's.
        RuleFor(command => command.PromotionId).NotEmpty();
    }
}