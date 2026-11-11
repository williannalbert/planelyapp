using PlanelyApp.Domain.Catalog.Promotions;

namespace PlanelyApp.Application.Catalog.Promotions;

public sealed record PromotionDto(
    Guid Id,
    PromotionKind Kind,
    string Name,
    string? Description,
    DiscountData Discount,
    DateOnly ValidFrom,
    DateOnly ValidUntil,
    IReadOnlyList<DayOfWeek> Weekdays,
    bool AppliesToAllBranches,
    IReadOnlyList<Guid> BranchIds,
    IReadOnlyList<Guid> ServiceIds,
    bool IsActive);