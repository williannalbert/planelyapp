namespace PlanelyApp.Application.Catalog.Pricing;

/// <summary>
/// The price of one service of the appointment. A service on request has no price. <see cref="IsAvailable"/> says
/// whether the service is active and offered by the branch: the price is shown either way.
/// </summary>
public sealed record PriceLineDto(
    Guid ServiceOfferingId,
    string ServiceName,
    bool IsOnRequest,
    decimal? ListPrice,
    decimal Discount,
    decimal? FinalPrice,
    Guid? PromotionId,
    string? PromotionName,
    bool IsFromBundle,
    bool IsAvailable);