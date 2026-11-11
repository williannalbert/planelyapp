using FluentValidation;

namespace PlanelyApp.Application.Catalog.Promotions.UpdateDetails;

internal sealed class UpdatePromotionDetailsValidator : AbstractValidator<UpdatePromotionDetailsCommand>
{
    public UpdatePromotionDetailsValidator()
    {
        // The lengths are the Domain's.
        RuleFor(command => command.PromotionId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty();
    }
}