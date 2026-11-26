using PlanelyApp.Api.Endpoints.Organization.Shared;
using PlanelyApp.Api.Errors;
using PlanelyApp.Api.Roles;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Organization.Professionals.Activate;
using PlanelyApp.Application.Organization.Professionals.AddTimeOff;
using PlanelyApp.Application.Organization.Professionals.Deactivate;
using PlanelyApp.Application.Organization.Professionals.DisableOffering;
using PlanelyApp.Application.Organization.Professionals.EnableOffering;
using PlanelyApp.Application.Organization.Professionals.Get;
using PlanelyApp.Application.Organization.Professionals.LinkUser;
using PlanelyApp.Application.Organization.Professionals.List;
using PlanelyApp.Application.Organization.Professionals.Register;
using PlanelyApp.Application.Organization.Professionals.RemoveTimeOff;
using PlanelyApp.Application.Organization.Professionals.RemoveWorkingHours;
using PlanelyApp.Application.Organization.Professionals.SetWorkingHours;
using PlanelyApp.Application.Organization.Professionals.UnlinkUser;
using PlanelyApp.Application.Organization.Professionals.UpdateDetails;
using PlanelyApp.Application.Organization.Shared;
using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Api.Endpoints.Organization.Professionals;

internal static class ProfessionalEndpoints
{
    // The platform administrator has no branch (RN11), so it is in none of these.
    private static readonly UserRole[] CompanyRoles = [UserRole.CompanyAdmin, UserRole.Receptionist, UserRole.Professional];

    // The agenda of a professional is organised at the front desk too; what the professional is, is for the administrator.
    private static readonly UserRole[] Agenda = [UserRole.CompanyAdmin, UserRole.Receptionist];

    public static IEndpointRouteBuilder MapProfessionals(this IEndpointRouteBuilder endpoints)
    {
        var professionals = endpoints.MapGroup("/companies/{companyId:guid}/professionals").WithTags("Professionals");

        professionals
            .MapGet("/", async (bool? isActive, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.QueryAsync(new ListProfessionalsQuery(isActive), cancellationToken)).ToHttp())
            .WithName("ListProfessionals")
            .WithSummary("Lists the professionals, with an optional filter of active or inactive.")
            .RequireRoles(CompanyRoles);

        professionals
            .MapPost("/", async (
                Guid companyId, RegisterProfessionalCommand command, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(command, cancellationToken))
                    .ToCreated(id => $"/companies/{companyId}/professionals/{id}"))
            .WithName("RegisterProfessional")
            .WithSummary("Registers a professional in the company.")
            .RequireRoles(UserRole.CompanyAdmin);

        var professional = professionals.MapGroup("/{professionalId:guid}");

        professional
            .MapGet("/", async (Guid professionalId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.QueryAsync(new GetProfessionalQuery(professionalId), cancellationToken)).ToHttp())
            .WithName("GetProfessional")
            .WithSummary("Reads a professional, with working hours, time offs and services.")
            .RequireRoles(CompanyRoles);

        // Every id of a command is the one of the route: a body that could bring its own would go around what the route says.
        professional
            .MapPut("/", async (
                Guid professionalId, ProfessionalDetailsBody body, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(
                    new UpdateProfessionalDetailsCommand(professionalId, body.Name, body.Specialty), cancellationToken)).ToNoContent())
            .WithName("UpdateProfessional")
            .WithSummary("Changes the name and the specialty of a professional.")
            .RequireRoles(UserRole.CompanyAdmin);

        professional
            .MapPost("/activate", async (Guid professionalId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(new ActivateProfessionalCommand(professionalId), cancellationToken)).ToNoContent())
            .WithName("ActivateProfessional")
            .WithSummary("Activates a professional.")
            .RequireRoles(UserRole.CompanyAdmin);

        professional
            .MapPost("/deactivate", async (Guid professionalId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(new DeactivateProfessionalCommand(professionalId), cancellationToken)).ToNoContent())
            .WithName("DeactivateProfessional")
            .WithSummary("Deactivates a professional.")
            .RequireRoles(UserRole.CompanyAdmin);

        // The branch is in the route on purpose: the tenancy checks it against the branches of the user (RN11) before the endpoint.
        professional
            .MapPut("/branches/{branchId:guid}/working-hours/{day}", async Task<IResult> (
                Guid professionalId,
                Guid branchId,
                DayOfWeek day,
                DailyScheduleBody body,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                if (!WeekdayRoute.IsValid(day))
                {
                    return WeekdayRoute.Invalid();
                }

                var schedule = new DailyScheduleData(day, body.Start, body.End, body.Breaks ?? []);

                return (await dispatcher.SendAsync(
                    new SetProfessionalWorkingHoursCommand(professionalId, branchId, schedule), cancellationToken)).ToNoContent();
            })
            .WithName("SetProfessionalWorkingHours")
            .WithSummary("Sets the working day of the professional at a branch.")
            .RequireRoles(Agenda);

        professional
            .MapDelete("/branches/{branchId:guid}/working-hours/{day}", async Task<IResult> (
                Guid professionalId,
                Guid branchId,
                DayOfWeek day,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                if (!WeekdayRoute.IsValid(day))
                {
                    return WeekdayRoute.Invalid();
                }

                return (await dispatcher.SendAsync(
                    new RemoveProfessionalWorkingHoursCommand(professionalId, branchId, day), cancellationToken)).ToNoContent();
            })
            .WithName("RemoveProfessionalWorkingHours")
            .WithSummary("Removes the working day of the professional at a branch.")
            .RequireRoles(Agenda);

        // There is no address for a single time off, so the Location is the professional, where it is read.
        professional
            .MapPost("/time-offs", async (
                Guid companyId,
                Guid professionalId,
                TimeOffBody body,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(
                    new AddProfessionalTimeOffCommand(professionalId, body.StartUtc, body.EndUtc, body.Reason), cancellationToken))
                    .ToCreated(_ => $"/companies/{companyId}/professionals/{professionalId}"))
            .WithName("AddProfessionalTimeOff")
            .WithSummary("Blocks a period (UTC) in which the professional is unavailable.")
            .RequireRoles(Agenda);

        professional
            .MapDelete("/time-offs/{timeOffId:guid}", async (
                Guid professionalId, Guid timeOffId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(
                    new RemoveProfessionalTimeOffCommand(professionalId, timeOffId), cancellationToken)).ToNoContent())
            .WithName("RemoveProfessionalTimeOff")
            .WithSummary("Removes a time off.")
            .RequireRoles(Agenda);

        professional
            .MapPut("/offerings/{serviceOfferingId:guid}", async (
                Guid professionalId,
                Guid serviceOfferingId,
                OfferingBody body,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(
                    new EnableProfessionalOfferingCommand(professionalId, serviceOfferingId, body.CustomDurationMinutes),
                    cancellationToken)).ToNoContent())
            .WithName("EnableProfessionalOffering")
            .WithSummary("The professional performs the service, with the standard duration or one of its own.")
            .RequireRoles(UserRole.CompanyAdmin);

        professional
            .MapDelete("/offerings/{serviceOfferingId:guid}", async (
                Guid professionalId, Guid serviceOfferingId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(
                    new DisableProfessionalOfferingCommand(professionalId, serviceOfferingId), cancellationToken)).ToNoContent())
            .WithName("DisableProfessionalOffering")
            .WithSummary("The professional stops performing the service.")
            .RequireRoles(UserRole.CompanyAdmin);

        professional
            .MapPut("/user", async (
                Guid professionalId, LinkUserBody body, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(
                    new LinkProfessionalUserCommand(professionalId, body.UserId), cancellationToken)).ToNoContent())
            .WithName("LinkProfessionalUser")
            .WithSummary("Links the professional to a user of the company. Linking another user replaces the link.")
            .RequireRoles(UserRole.CompanyAdmin);

        professional
            .MapDelete("/user", async (Guid professionalId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(new UnlinkProfessionalUserCommand(professionalId), cancellationToken)).ToNoContent())
            .WithName("UnlinkProfessionalUser")
            .WithSummary("Removes the link to the user.")
            .RequireRoles(UserRole.CompanyAdmin);

        return endpoints;
    }
}