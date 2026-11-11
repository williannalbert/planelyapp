using FluentValidation;

namespace PlanelyApp.Application.Catalog.Promotions.SetWeekdays;

internal sealed class SetPromotionWeekdaysValidator : AbstractValidator<SetPromotionWeekdaysCommand>
{
    public SetPromotionWeekdaysValidator()
    {
        // A list is always sent, even an empty one; that each day is a real weekday is the Domain's.
        RuleFor(command => command.PromotionId).NotEmpty();
        RuleFor(command => command.Weekdays).NotNull();
    }
}