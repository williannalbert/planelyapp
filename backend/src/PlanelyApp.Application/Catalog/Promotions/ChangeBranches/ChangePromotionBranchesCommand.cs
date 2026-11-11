using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Catalog.Promotions.ChangeBranches;

/// <summary>
/// Replaces where a promotion applies: all branches of the company, or only the ones listed.
/// Same shape as in the creation: "all branches" comes without a list, "some branches" comes with one.
/// </summary>
public sealed record ChangePromotionBranchesCommand(
    Guid PromotionId,
    bool AppliesToAllBranches,
    IReadOnlyList<Guid>? BranchIds = null) : ICommand<Unit>;