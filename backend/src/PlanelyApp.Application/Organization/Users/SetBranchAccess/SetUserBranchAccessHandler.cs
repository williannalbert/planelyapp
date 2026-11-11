using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Organization.Users.SetBranchAccess;

internal sealed class SetUserBranchAccessHandler(
    IUserRepository users,
    IBranchRepository branches,
    ITenantProvider tenantProvider) : ICommandHandler<SetUserBranchAccessCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        SetUserBranchAccessCommand command,
        CancellationToken cancellationToken)
    {
        var user = await users.GetByIdInCompanyAsync(tenantProvider.RequireCompanyId(), command.UserId, cancellationToken);

        if (user is null)
        {
            return Error.NotFound("User.NotFound");
        }

        var branchIds = (command.BranchIds ?? []).Distinct().ToList();

        if (await UserBranchAccess.FindMissingBranchAsync(
                branches, command.AccessAllBranches, branchIds, cancellationToken) is { } missing)
        {
            return missing;
        }

        user.SetBranchAccess(command.AccessAllBranches, branchIds);

        return Result.Success();
    }
}