using PlanelyApp.Api.Endpoints.Organization.Shared;
using PlanelyApp.Api.Errors;
using PlanelyApp.Api.Roles;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Organization.Users.Activate;
using PlanelyApp.Application.Organization.Users.ChangeRole;
using PlanelyApp.Application.Organization.Users.Create;
using PlanelyApp.Application.Organization.Users.Deactivate;
using PlanelyApp.Application.Organization.Users.Get;
using PlanelyApp.Application.Organization.Users.List;
using PlanelyApp.Application.Organization.Users.SetBranchAccess;
using PlanelyApp.Application.Organization.Users.UpdateDetails;
using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Api.Endpoints.Organization.Users;

internal static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUsers(this IEndpointRouteBuilder endpoints)
    {
        // The accounts of the team and who may do what are for the administrator alone, so the rule is the one of the group.
        var users = endpoints.MapGroup("/companies/{companyId:guid}/users")
            .WithTags("Users")
            .RequireRoles(UserRole.CompanyAdmin);

        users
            .MapGet("/", async Task<IResult> (
                UserRole? role, bool? isActive, IDispatcher dispatcher, CancellationToken cancellationToken) =>
            {
                // The binder reads "9" as a role that does not exist, and the name of a role is the contract.
                if (role is { } value && !Enum.IsDefined(value))
                {
                    return FieldProblems.Invalid(nameof(ListUsersQuery.Role));
                }

                return (await dispatcher.QueryAsync(new ListUsersQuery(role, isActive), cancellationToken)).ToHttp();
            })
            .WithName("ListUsers")
            .WithSummary("Lists the users of the company, with optional filters of role and active or inactive.");

        users
            .MapPost("/", async Task<IResult> (
                Guid companyId, CreateUserBody body, IDispatcher dispatcher, CancellationToken cancellationToken) =>
            {
                // Left out, the role would be PlatformAdmin (the first member of the enum), after the account was created.
                if (body.Role is not { } role)
                {
                    return FieldProblems.Required(nameof(CreateUserCommand.Role));
                }

                var command = new CreateUserCommand(
                    body.Name, body.Email, role, body.AccessAllBranches, body.BranchIds, body.PreferredLanguage);

                return (await dispatcher.SendAsync(command, cancellationToken))
                    .ToCreated(id => $"/companies/{companyId}/users/{id}");
            })
            .WithName("CreateUser")
            .WithSummary("Creates the login account and the user, with the branches it sees.");

        var user = users.MapGroup("/{userId:guid}");

        user
            .MapGet("/", async (Guid userId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.QueryAsync(new GetUserQuery(userId), cancellationToken)).ToHttp())
            .WithName("GetUser")
            .WithSummary("Reads a user, with the branches it sees.");

        user
            .MapPut("/", async (
                Guid userId, UserDetailsBody body, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(
                    new UpdateUserDetailsCommand(userId, body.Name, body.Email, body.PreferredLanguage), cancellationToken))
                    .ToNoContent())
            .WithName("UpdateUser")
            .WithSummary("Changes the name, the e-mail and the language of a user, here and at the identity provider.");

        user
            .MapPut("/role", async Task<IResult> (
                Guid userId, UserRoleBody body, IDispatcher dispatcher, CancellationToken cancellationToken) =>
            {
                if (body.Role is not { } role)
                {
                    return FieldProblems.Required(nameof(ChangeUserRoleCommand.Role));
                }

                return (await dispatcher.SendAsync(new ChangeUserRoleCommand(userId, role), cancellationToken)).ToNoContent();
            })
            .WithName("ChangeUserRole")
            .WithSummary("Changes the role of a user among the company roles.");

        user
            .MapPut("/branch-access", async Task<IResult> (
                Guid userId, BranchAccessBody body, IDispatcher dispatcher, CancellationToken cancellationToken) =>
            {
                if (body.AccessAllBranches is not { } accessAll)
                {
                    return FieldProblems.Required(nameof(SetUserBranchAccessCommand.AccessAllBranches));
                }

                return (await dispatcher.SendAsync(
                    new SetUserBranchAccessCommand(userId, accessAll, body.BranchIds), cancellationToken)).ToNoContent();
            })
            .WithName("SetUserBranchAccess")
            .WithSummary("Replaces the whole branch access of a user: every branch, or exactly the list.");

        user
            .MapPost("/activate", async (Guid userId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(new ActivateUserCommand(userId), cancellationToken)).ToNoContent())
            .WithName("ActivateUser")
            .WithSummary("Activates a user and enables its account.");

        user
            .MapPost("/deactivate", async (Guid userId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(new DeactivateUserCommand(userId), cancellationToken)).ToNoContent())
            .WithName("DeactivateUser")
            .WithSummary("Deactivates a user and disables its account. The last administrator cannot be deactivated.");

        return endpoints;
    }
}