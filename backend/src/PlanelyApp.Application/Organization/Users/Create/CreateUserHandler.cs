using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Application.Abstractions.Validation;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Organization.Users.Create;

internal sealed class CreateUserHandler(
    IUserRepository users,
    IBranchRepository branches,
    ICompanyRepository companies,
    IIdentityProvider identityProvider,
    ITenantProvider tenantProvider) : ICommandHandler<CreateUserCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var companyId = tenantProvider.RequireCompanyId();

        // Checked before the account exists: a company that is not there would fail at the foreign key, after it.
        if (await companies.GetByIdAsync(companyId, cancellationToken) is null)
        {
            return Error.NotFound("Company.NotFound");
        }

        var email = Email.Create(command.Email);

        if (await users.FindByEmailAsync(companyId, email, cancellationToken) is not null)
        {
            return Error.Conflict("User.EmailAlreadyRegistered");
        }

        var branchIds = (command.BranchIds ?? []).Distinct().ToList();

        if (await UserBranchAccess.FindMissingBranchAsync(
                branches, command.AccessAllBranches, branchIds, cancellationToken) is { } missing)
        {
            return missing;
        }

        var language = command.PreferredLanguage.OrNull(Language.Create);
        var name = command.Name.Trim();

        // Last, because it is the one step that changes something outside the database.
        var account = await identityProvider.CreateUserAsync(new IdentityProfile(name, email, language), cancellationToken);

        if (account.IsFailure)
        {
            return account.Error;
        }

        User user;

        try
        {
            user = User.CreateForCompany(companyId, account.Value, name, email, command.Role, language);
            user.SetBranchAccess(command.AccessAllBranches, branchIds);
        }
        catch
        {
            // The Domain refused (an invalid role, a name too long): the account must not stay behind.
            await identityProvider.DeleteUserAsync(account.Value, cancellationToken);

            throw;
        }

        users.Add(user);

        return user.Id;
    }
}