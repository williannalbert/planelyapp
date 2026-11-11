using PlanelyApp.Domain.Catalog.ServiceOfferings;

namespace PlanelyApp.Application.Catalog.BranchOfferings;

/// <summary>
/// A service as one branch has it. <see cref="CustomPrice"/> is the price the branch gave it (only a fixed price service
/// can have one); <see cref="Price"/> is the one that applies there: the branch price, else the base price, and none
/// for a service on request.
/// </summary>
public sealed record BranchOfferingDto(
    Guid ServiceOfferingId,
    string Name,
    Guid? CategoryId,
    string? CategoryName,
    int DurationMinutes,
    PricingType PricingType,
    decimal? BasePrice,
    decimal? CustomPrice,
    decimal? Price,
    bool IsAvailable);