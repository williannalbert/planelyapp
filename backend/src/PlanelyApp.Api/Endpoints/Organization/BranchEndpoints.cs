using PlanelyApp.Api.Errors;
using PlanelyApp.Api.Roles;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Branches.Activate;
using PlanelyApp.Application.Organization.Branches.Create;
using PlanelyApp.Application.Organization.Branches.Deactivate;
using PlanelyApp.Application.Organization.Branches.Get;
using PlanelyApp.Application.Organization.Branches.List;
using PlanelyApp.Application.Organization.Branches.RemoveBusinessHours;
using PlanelyApp.Application.Organization.Branches.SetBusinessHours;
using PlanelyApp.Application.Organization.Branches.UpdateDetails;
using PlanelyApp.Application.Organization.Shared;
using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Api.Endpoints.Organization;

internal static class BranchEndpoints
{
    // The platform administrator has no branch (RN11), so it is in none of these.
    private static readonly UserRole[] CompanyRoles = [UserRole.CompanyAdmin, UserRole.Receptionist, UserRole.Professional];

    public static IEndpointRouteBuilder MapBranches(this IEndpointRouteBuilder endpoints)
    {
        var branches = endpoints.MapGroup("/companies/{companyId:guid}/branches").WithTags("Branches");

        branches
            .MapGet("/", async (bool? isActive, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.QueryAsync(new ListBranchesQuery(isActive), cancellationToken)).ToHttp())
            .WithName("ListBranches")
            .WithSummary("Lists the branches the user may see, with an optional filter of active or inactive.")
            .RequireRoles(CompanyRoles);

        branches
            .MapPost("/", async (
                Guid companyId, CreateBranchCommand command, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(command, cancellationToken))
                    .ToCreated(id => $"/companies/{companyId}/branches/{id}"))
            .WithName("CreateBranch")
            .WithSummary("Creates a branch in the company.")
            .RequireRoles(UserRole.CompanyAdmin);

        var branch = branches.MapGroup("/{branchId:guid}");

        branch
            .MapGet("/", async (Guid branchId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.QueryAsync(new GetBranchQuery(branchId), cancellationToken)).ToHttp())
            .WithName("GetBranch")
            .WithSummary("Reads a branch, with its address and business hours.")
            .RequireRoles(CompanyRoles);

        // The id of the command is the one of the route: a body that could bring its own would go around the check of the tenancy.
        branch
            .MapPut("/", async (
                Guid branchId, BranchDetailsBody body, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(
                    new UpdateBranchDetailsCommand(branchId, body.Name, body.Address, body.Phone, body.TimeZoneId),
                    cancellationToken)).ToNoContent())
            .WithName("UpdateBranch")
            .WithSummary("Changes the name, address, phone and time zone of a branch.")
            .RequireRoles(UserRole.CompanyAdmin);

        branch
            .MapPost("/activate", async (Guid branchId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(new ActivateBranchCommand(branchId), cancellationToken)).ToNoContent())
            .WithName("ActivateBranch")
            .WithSummary("Activates a branch.")
            .RequireRoles(UserRole.CompanyAdmin);

        branch
            .MapPost("/deactivate", async (Guid branchId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(new DeactivateBranchCommand(branchId), cancellationToken)).ToNoContent())
            .WithName("DeactivateBranch")
            .WithSummary("Deactivates a branch.")
            .RequireRoles(UserRole.CompanyAdmin);

        branch
            .MapPut("/business-hours/{day}", async Task<IResult> (
                Guid branchId,
                DayOfWeek day,
                BusinessHoursBody body,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                if (!Enum.IsDefined(day))
                {
                    return InvalidDay();
                }

                var schedule = new DailyScheduleData(day, body.Start, body.End, body.Breaks ?? []);

                return (await dispatcher.SendAsync(
                    new SetBranchBusinessHoursCommand(branchId, schedule), cancellationToken)).ToNoContent();
            })
            .WithName("SetBranchBusinessHours")
            .WithSummary("Opens a weekday of the branch, or replaces its hours and breaks.")
            .RequireRoles(UserRole.CompanyAdmin);

        branch
            .MapDelete("/business-hours/{day}", async Task<IResult> (
                Guid branchId, DayOfWeek day, IDispatcher dispatcher, CancellationToken cancellationToken) =>
            {
                if (!Enum.IsDefined(day))
                {
                    return InvalidDay();
                }

                return (await dispatcher.SendAsync(
                    new RemoveBranchBusinessHoursCommand(branchId, day), cancellationToken)).ToNoContent();
            })
            .WithName("RemoveBranchBusinessHours")
            .WithSummary("Closes the branch on a weekday.")
            .RequireRoles(UserRole.CompanyAdmin);

        return endpoints;
    }

    // The route binds a number to the enum without checking that it is a weekday.
    private static IResult InvalidDay() =>
        new ErrorResult(new ValidationError([new FieldError("day", "Validation.Invalid")]));
}