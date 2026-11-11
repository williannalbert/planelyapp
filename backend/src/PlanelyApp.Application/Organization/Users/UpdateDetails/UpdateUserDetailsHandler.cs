using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Application.Abstractions.Validation;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Organization.Users.UpdateDetails;

internal sealed class UpdateUserDetailsHandler(
    IUserRepository users,
    IIdentityProvider identityProvider,
    ITenantProvider tenantProvider) : ICommandHandler<UpdateUserDetailsCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(UpdateUserDetailsCommand command, CancellationToken cancellationToken)
    {
        var companyId = tenantProvider.RequireCompanyId();
        var user = await users.GetByIdInCompanyAsync(companyId, command.UserId, cancellationToken);

        if (user is null)
        {
            return Error.NotFound("User.NotFound");
        }

        var email = Email.Create(command.Email);

        // Finding the user itself is not a clash: keeping the e-mail must not fail.
        if (await users.FindByEmailAsync(companyId, email, cancellationToken) is { } other && other.Id != user.Id)
        {
            return Error.Conflict("User.EmailAlreadyRegistered");
        }

        var previousName = user.Name;
        var previousEmail = user.Email;
        var previousLanguage = user.PreferredLanguage;

        // The Domain first: if it refuses, nothing outside the database was touched.
        user.UpdateDetails(command.Name, email, command.PreferredLanguage.OrNull(Language.Create));

        var changed = previousName != user.Name
            || !previousEmail.Equals(user.Email)
            || !object.Equals(previousLanguage, user.PreferredLanguage);

        if (changed)
        {
            var updated = await identityProvider.UpdateUserAsync(
                user.IdentityId,
                new IdentityProfile(user.Name, user.Email, user.PreferredLanguage),
                cancellationToken);

            if (updated.IsFailure)
            {
                // Nothing is saved: the change made to the user above is dropped with the request.
                return updated.Error;
            }
        }

        return Result.Success();
    }
}