using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Organization.Branches;

namespace PlanelyApp.Application.Catalog.Promotions.ChangeBranches;

internal sealed class ChangePromotionBranchesHandler(IPromotionRepository promotions, IBranchRepository branches)
    : ICommandHandler<ChangePromotionBranchesCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        ChangePromotionBranchesCommand command,
        CancellationToken cancellationToken)
    {
        var promotion = await promotions.GetByIdAsync(command.PromotionId, cancellationToken);

        if (promotion is null)
        {
            return Error.NotFound("Promotion.NotFound");
        }

        if (command.AppliesToAllBranches)
        {
            promotion.ApplyToAllBranches();

            return Result.Success();
        }

        // The validator guarantees a list here. That the branches exist in this company is checked here, with one query.
        var branchIds = command.BranchIds!.Distinct().ToList();

        if (await branches.CountByIdsAsync(branchIds, cancellationToken) != branchIds.Count)
        {
            return Error.NotFound("Branch.NotFound");
        }

        promotion.ApplyToBranches(branchIds);

        return Result.Success();
    }
}