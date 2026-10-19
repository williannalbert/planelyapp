namespace PlanelyApp.Domain.Catalog.Promotions;

public enum DiscountType
{
    /// <summary>A percentage of the price, greater than 0 and up to 100.</summary>
    Percentage,

    /// <summary>A fixed amount taken off the price.</summary>
    FixedAmount,
}