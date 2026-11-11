using FluentValidation;

namespace PlanelyApp.Application.Catalog.Promotions.ChangeDiscount;

internal sealed class ChangePromotionDiscountValidator : AbstractValidator<ChangePromotionDiscountCommand>
{
    public ChangePromotionDiscountValidator()
    {
        RuleFor(command => command.PromotionId).NotEmpty();
        RuleFor(command => command.Discount).NotNull().SetValidator(new DiscountDataValidator());
    }
}