using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Domain.Catalog.Promotions;

/// <summary>How much a promotion takes off the price (RN16).</summary>
public sealed class Discount : ValueObject
{
    private Discount(DiscountType type, decimal value)
    {
        Type = type;
        Value = value;
    }

    public DiscountType Type { get; }

    /// <summary>A percentage (0 to 100) or an amount of money, depending on the type.</summary>
    public decimal Value { get; }

    public static Discount Percentage(decimal percent)
    {
        if (percent is <= 0 or > 100)
        {
            throw new DomainException("Discount.PercentageInvalid", percent);
        }

        return new Discount(DiscountType.Percentage, percent);
    }

    public static Discount FixedAmount(Money amount)
    {
        if (amount <= Money.Zero)
        {
            throw new DomainException("Discount.FixedAmountInvalid");
        }

        return new Discount(DiscountType.FixedAmount, amount.Amount);
    }

    /// <summary>
    /// The amount taken off the given price. Never more than the price itself,
    /// so the final price is never negative (RN16).
    /// </summary>
    public Money CalculateOn(Money price)
    {
        var amount = Type == DiscountType.Percentage
            ? price.Percentage(Value)
            : Money.From(Value);

        return amount > price ? price : amount;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Type;
        yield return Value;
    }
}