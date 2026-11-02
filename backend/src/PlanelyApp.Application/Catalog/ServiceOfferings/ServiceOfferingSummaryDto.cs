using PlanelyApp.Domain.Catalog.ServiceOfferings;

namespace PlanelyApp.Application.Catalog.ServiceOfferings;

/// <summary>What a list needs: no description, and the name of the category so the screen does not look it up.</summary>
public sealed record ServiceOfferingSummaryDto(
    Guid Id,
    string Name,
    Guid? CategoryId,
    string? CategoryName,
    int DurationMinutes,
    PricingType PricingType,
    decimal? BasePrice,
    bool IsActive);