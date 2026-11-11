using FluentValidation;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Catalog.Promotions;

/// <summary>How much a promotion takes off: a percentage (above 0, up to 100) or an amount of money.</summary>
public sealed record DiscountData(DiscountType Type, decimal Value);

internal sealed class DiscountDataValidator : AbstractValidator<DiscountData>
{
    public DiscountDataValidator()
    {
        RuleFor(discount => discount.Type).IsInEnum();

        // The column is decimal(12,2). Whether the value is above zero (and up to 100 for a percentage) is the Domain's.
        RuleFor(discount => discount.Value).PrecisionScale(12, 2, true);
    }
}

internal static class DiscountDataExtensions
{
    public static Discount ToDomain(this DiscountData data) =>
        data.Type == DiscountType.Percentage
            ? Discount.Percentage(data.Value)
            : Discount.FixedAmount(Money.From(data.Value));
}