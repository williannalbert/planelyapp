using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Scheduling.Appointments;

/// <summary>
/// Loads an appointment for the use cases that change it, and checks that who is acting may see it. The Domain decides
/// whether the change itself is allowed; this is only about data the Api cannot see (RN11).
/// </summary>
internal sealed class AppointmentAccess(
    IAppointmentRepository appointments,
    IUserRepository users,
    ICurrentUser currentUser,
    ITenantProvider tenantProvider)
{
    /// <summary>For what only the staff does: confirm, start, complete, no-show.</summary>
    public async Task<Result<Appointment>> LoadForStaffAsync(Guid appointmentId, CancellationToken cancellationToken)
    {
        // Costs no query, so it comes first.
        if (currentUser.Kind != ActorKind.Staff)
        {
            return Error.Forbidden("Appointment.StaffOnly");
        }

        var appointment = await appointments.GetByIdAsync(appointmentId, cancellationToken);

        if (appointment is null)
        {
            return Error.NotFound("Appointment.NotFound");
        }

        if (!await StaffCanAccessAsync(appointment.BranchId, cancellationToken))
        {
            return Error.Forbidden("Branch.AccessDenied");
        }

        return appointment;
    }

    /// <summary>For what the customer can also do, such as cancel: the staff of the branch or the customer of the appointment.</summary>
    public async Task<Result<Appointment>> LoadForActorAsync(Guid appointmentId, CancellationToken cancellationToken)
    {
        var appointment = await appointments.GetByIdAsync(appointmentId, cancellationToken);

        if (appointment is null)
        {
            return Error.NotFound("Appointment.NotFound");
        }

        if (currentUser.Kind == ActorKind.Customer)
        {
            // Another customer's appointment does not exist for this one: a 403 would say that it does.
            return appointment.CustomerId == currentUser.Id
                ? appointment
                : Error.NotFound("Appointment.NotFound");
        }

        if (!await StaffCanAccessAsync(appointment.BranchId, cancellationToken))
        {
            return Error.Forbidden("Branch.AccessDenied");
        }

        return appointment;
    }

    // A user has no tenant filter, so the lookup names the company (RN23). Whether the user may use the branch is RN11.
    private async Task<bool> StaffCanAccessAsync(Guid branchId, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdInCompanyAsync(tenantProvider.RequireCompanyId(), currentUser.Id, cancellationToken);

        return user is not null && user.CanAccess(branchId);
    }
}