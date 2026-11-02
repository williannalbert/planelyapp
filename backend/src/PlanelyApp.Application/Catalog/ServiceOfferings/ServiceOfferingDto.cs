using PlanelyApp.Domain.Catalog.ServiceOfferings;

namespace PlanelyApp.Application.Catalog.ServiceOfferings;

public sealed record ServiceOfferingDto(
    Guid Id,
    string Name,
    string? Description,
    int DurationMinutes,
    PricingType PricingType,
    decimal? BasePrice,
    Guid? CategoryId,
    bool IsActive);