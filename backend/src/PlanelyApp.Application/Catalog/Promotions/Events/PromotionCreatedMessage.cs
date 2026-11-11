namespace PlanelyApp.Application.Catalog.Promotions.Events;

/// <summary>When it applies to all branches, <see cref="BranchIds"/> is empty and the consumer targets every branch.</summary>
internal sealed record PromotionCreatedMessage(
    Guid PromotionId,
    bool AppliesToAllBranches,
    IReadOnlyList<Guid> BranchIds);