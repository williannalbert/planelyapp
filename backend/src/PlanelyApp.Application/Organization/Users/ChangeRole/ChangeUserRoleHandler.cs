using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Organization.Users.ChangeRole;

internal sealed class ChangeUserRoleHandler(IUserRepository users, ITenantProvider tenantProvider)
    : ICommandHandler<ChangeUserRoleCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(ChangeUserRoleCommand command, CancellationToken cancellationToken)
    {
        var companyId = tenantProvider.RequireCompanyId();
        var user = await users.GetByIdInCompanyAsync(companyId, command.UserId, cancellationToken);

        if (user is null)
        {
            return Error.NotFound("User.NotFound");
        }

        var wasActiveAdministrator = user is { Role: UserRole.CompanyAdmin, IsActive: true };

        // The Domain refuses a role that cannot be given (a 422) before the rule that looks at the other users.
        user.ChangeRole(command.Role);

        if (wasActiveAdministrator
            && command.Role != UserRole.CompanyAdmin
            && await users.CountActiveAdministratorsAsync(companyId, cancellationToken) <= 1)
        {
            return Error.Conflict("User.LastAdministrator");
        }

        return Result.Success();
    }
}