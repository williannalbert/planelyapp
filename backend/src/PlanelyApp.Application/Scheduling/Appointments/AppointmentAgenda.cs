using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.Scheduling.Availability;

namespace PlanelyApp.Application.Scheduling.Appointments;

/// <summary>
/// For the changes to an appointment that already exists and can take more of the agenda: moving it, or making it longer
/// by agreeing on the duration of a service on request. The Domain has already changed the period; this checks that the
/// new period fits and raises the schedule version in the same transaction (RN27).
/// </summary>
internal sealed class AppointmentAgenda(
    IBranchRepository branches,
    IProfessionalRepository professionals,
    IAppointmentRepository appointments)
{
    /// <param name="appointment">Already changed by the Domain.</param>
    /// <param name="needsRoom">
    /// False when the change cannot take agenda away from anyone (the period did not grow): nothing to check.
    /// </param>
    public async Task<Result<Unit>> RegisterChangeAsync(
        Appointment appointment,
        BookingPolicy policy,
        bool needsRoom,
        CancellationToken cancellationToken)
    {
        var professional = await professionals.GetByIdAsync(appointment.ProfessionalId, cancellationToken);

        if (professional is null)
        {
            return Error.NotFound("Professional.NotFound");
        }

        if (needsRoom)
        {
            var branch = await branches.GetByIdAsync(appointment.BranchId, cancellationToken);

            if (branch is null)
            {
                return Error.NotFound("Branch.NotFound");
            }

            if (!branch.IsActive)
            {
                return Error.Conflict("Branch.Inactive");
            }

            if (!professional.IsActive)
            {
                return Error.Conflict("Professional.Inactive");
            }

            // Only what touches the period, with the travel time to and from other branches, can stop it from fitting.
            var travel = TimeSpan.FromMinutes(policy.TravelTimeBetweenBranchesMinutes);
            var booked = await appointments.ListOccupyingScheduleAsync(
                [professional.Id],
                appointment.Period.Start - travel,
                appointment.Period.End + travel,
                cancellationToken);

            // The appointment itself is left out: it is in the list with its new period when it is already loaded.
            if (!AvailabilityCalculator.IsAvailable(
                    branch, professional, appointment.Period, booked, policy, appointment.Id))
            {
                return Error.Conflict("Appointment.TimeNotAvailable");
            }
        }

        // If someone changed this professional after we read it, the save fails with DbUpdateConcurrencyException.
        professional.IncrementScheduleVersion();

        return Result.Success();
    }
}