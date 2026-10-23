using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.Catalog.Promotions;

/// <summary>
/// A promotion was created. When AppliesToAllBranches is true, BranchIds is empty
/// and the consumer must target every branch of the company.
/// </summary>
public sealed record PromotionCreated(
    Guid PromotionId,
    Guid CompanyId,
    bool AppliesToAllBranches,
    IReadOnlyList<Guid> BranchIds) : DomainEvent;