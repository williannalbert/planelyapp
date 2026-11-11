using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Organization.Users.Get;

internal sealed class GetUserHandler(IUserRepository users, ITenantProvider tenantProvider)
    : IQueryHandler<GetUserQuery, UserDto>
{
    public async Task<Result<UserDto>> HandleAsync(GetUserQuery query, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdInCompanyAsync(tenantProvider.RequireCompanyId(), query.UserId, cancellationToken);

        if (user is null)
        {
            return Error.NotFound("User.NotFound");
        }

        return new UserDto(
            user.Id,
            user.Name,
            user.Email.Value,
            user.Role,
            user.AccessAllBranches,
            user.AllowedBranchIds.Order().ToList(),
            user.PreferredLanguage?.Code,
            user.IsActive);
    }
}