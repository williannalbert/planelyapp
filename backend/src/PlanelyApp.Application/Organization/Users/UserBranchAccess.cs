using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Branches;

namespace PlanelyApp.Application.Organization.Users;

internal static class UserBranchAccess
{
    /// <summary>
    /// The error for the first branch that is not in the company of the request, or null when all of them are (or the
    /// user sees every branch, so none is needed).
    /// </summary>
    public static async Task<Error?> FindMissingBranchAsync(
        IBranchRepository branches,
        bool accessAllBranches,
        IEnumerable<Guid> branchIds,
        CancellationToken cancellationToken)
    {
        if (accessAllBranches)
        {
            return null;
        }

        foreach (var branchId in branchIds)
        {
            if (await branches.GetByIdAsync(branchId, cancellationToken) is null)
            {
                return Error.NotFound("Branch.NotFound");
            }
        }

        return null;
    }
}