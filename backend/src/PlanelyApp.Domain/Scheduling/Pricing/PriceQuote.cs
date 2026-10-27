using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Domain.Scheduling.Pricing;

/// <summary>
/// The price of a service in a branch at a given moment. For services on request there is no price yet.
/// </summary>
public sealed class PriceQuote : ValueObject
{
    private PriceQuote(bool isOnRequest, Money? listPrice, Money discount, Guid? promotionId, bool isFromBundle)
    {
        IsOnRequest = isOnRequest;
        ListPrice = listPrice;
        Discount = discount;
        PromotionId = promotionId;
        IsFromBundle = isFromBundle;
        FinalPrice = listPrice?.Subtract(discount);
    }

    public bool IsOnRequest { get; }

    /// <summary>Price before any promotion: the branch price if it has one, otherwise the base price (RN13).</summary>
    public Money? ListPrice { get; }

    /// <summary>The discount on this service. For a bundle, this service's share of the bundle discount.</summary>
    public Money Discount { get; }

    /// <summary>List price minus discount. Null for services on request.</summary>
    public Money? FinalPrice { get; }

    public Guid? PromotionId { get; }

    /// <summary>Whether the discount came from a bundle, so it depends on the other services of the appointment.</summary>
    public bool IsFromBundle { get; }

    public static PriceQuote OnRequest() => new(true, null, Money.Zero, null, false);

    public static PriceQuote Fixed(Money listPrice) => new(false, listPrice, Money.Zero, null, false);

    public static PriceQuote Fixed(Money listPrice, Money discount, Guid promotionId)
    {
        if (promotionId == Guid.Empty)
        {
            throw new DomainException("PriceQuote.PromotionRequired");
        }

        // Throws Money.NegativeAmount if the discount is larger than the price.
        return new PriceQuote(false, listPrice, discount, promotionId, false);
    }

    /// <summary>
    /// The price of a service that is part of a bundle: <paramref name="discountShare"/> is its share
    /// of the bundle discount. A share of zero is valid (a very cheap service in the bundle).
    /// </summary>
    public static PriceQuote FromBundle(Money listPrice, Money discountShare, Guid promotionId)
    {
        if (promotionId == Guid.Empty)
        {
            throw new DomainException("PriceQuote.PromotionRequired");
        }

        return new PriceQuote(false, listPrice, discountShare, promotionId, true);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return IsOnRequest;
        yield return ListPrice;
        yield return Discount;
        yield return PromotionId;
        yield return IsFromBundle;
    }
}