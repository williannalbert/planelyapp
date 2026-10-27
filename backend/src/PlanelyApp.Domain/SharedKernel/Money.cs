using System.Globalization;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.SharedKernel;

public sealed class Money : ValueObject, IComparable<Money>
{
    public static readonly Money Zero = new(0m);

    private Money(decimal amount) => Amount = amount;

    public decimal Amount { get; }

    public static Money From(decimal amount)
    {
        if (amount < 0)
        {
            throw new DomainException("Money.NegativeAmount");
        }

        return new Money(Math.Round(amount, 2, MidpointRounding.AwayFromZero));
    }

    public Money Add(Money other) => From(Amount + other.Amount);

    public Money Subtract(Money other) => From(Amount - other.Amount);

    public Money Percentage(decimal percent)
    {
        if (percent is < 0 or > 100)
        {
            throw new DomainException("Money.PercentageInvalid", percent);
        }

        return From(Amount * percent / 100m);
    }

    public int CompareTo(Money? other) => other is null ? 1 : Amount.CompareTo(other.Amount);

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    public override string ToString() => Amount.ToString("F2", CultureInfo.InvariantCulture);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
    }
}