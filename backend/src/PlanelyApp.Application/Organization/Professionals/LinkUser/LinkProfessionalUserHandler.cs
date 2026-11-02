using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Organization.Professionals.LinkUser;

internal sealed class LinkProfessionalUserHandler(
    IProfessionalRepository professionals,
    IUserRepository users,
    ITenantProvider tenantProvider) : ICommandHandler<LinkProfessionalUserCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        LinkProfessionalUserCommand command,
        CancellationToken cancellationToken)
    {
        var professional = await professionals.GetByIdAsync(command.ProfessionalId, cancellationToken);

        if (professional is null)
        {
            return Error.NotFound("Professional.NotFound");
        }

        // A user has no tenant filter, so the company is asked for: a user of another company is not found.
        var user = await users.GetByIdInCompanyAsync(tenantProvider.RequireCompanyId(), command.UserId, cancellationToken);

        if (user is null)
        {
            return Error.NotFound("User.NotFound");
        }

        // The Domain cannot see the other professionals. Finding this same professional is not a clash.
        if (await professionals.FindByUserIdAsync(user.Id, cancellationToken) is { } linked
            && linked.Id != professional.Id)
        {
            return Error.Conflict("Professional.UserAlreadyLinked");
        }

        professional.LinkUser(user.Id);

        return Result.Success();
    }
}