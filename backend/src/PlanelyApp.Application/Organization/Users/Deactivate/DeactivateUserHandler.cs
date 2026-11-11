using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Organization.Users.Deactivate;

internal sealed class DeactivateUserHandler(
    IUserRepository users,
    IIdentityProvider identityProvider,
    ITenantProvider tenantProvider) : ICommandHandler<DeactivateUserCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(DeactivateUserCommand command, CancellationToken cancellationToken)
    {
        var companyId = tenantProvider.RequireCompanyId();
        var user = await users.GetByIdInCompanyAsync(companyId, command.UserId, cancellationToken);

        if (user is null)
        {
            return Error.NotFound("User.NotFound");
        }

        if (user is { Role: UserRole.CompanyAdmin, IsActive: true }
            && await users.CountActiveAdministratorsAsync(companyId, cancellationToken) <= 1)
        {
            return Error.Conflict("User.LastAdministrator");
        }

        user.Deactivate();

        // The local flag is what blocks requests right away; this stops new logins. Repeating it is harmless.
        await identityProvider.SetEnabledAsync(user.IdentityId, false, cancellationToken);

        return Result.Success();
    }
}