using PlanelyApp.Domain.Catalog.Promotions;

namespace PlanelyApp.Application.Catalog.Promotions;

/// <summary>What a list needs: no description, no ids, only how many services the promotion covers.</summary>
public sealed record PromotionSummaryDto(
    Guid Id,
    PromotionKind Kind,
    string Name,
    DiscountType DiscountType,
    decimal DiscountValue,
    DateOnly ValidFrom,
    DateOnly ValidUntil,
    bool AppliesToAllBranches,
    int ServiceCount,
    bool IsActive);