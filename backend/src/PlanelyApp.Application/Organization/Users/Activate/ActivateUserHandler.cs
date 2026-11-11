using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Organization.Users.Activate;

internal sealed class ActivateUserHandler(
    IUserRepository users,
    IIdentityProvider identityProvider,
    ITenantProvider tenantProvider) : ICommandHandler<ActivateUserCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(ActivateUserCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdInCompanyAsync(tenantProvider.RequireCompanyId(), command.UserId, cancellationToken);

        if (user is null)
        {
            return Error.NotFound("User.NotFound");
        }

        user.Activate();

        // Always called, even if the user was already active: a retry repairs an account left out of step.
        await identityProvider.SetEnabledAsync(user.IdentityId, true, cancellationToken);

        return Result.Success();
    }
}