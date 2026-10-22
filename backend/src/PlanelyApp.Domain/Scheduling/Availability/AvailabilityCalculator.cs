using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Domain.Scheduling.Availability;

/// <summary>
/// Domain service: works out when a professional can be booked at a branch (RN01, RN02).
/// Pure: the caller loads the appointments of the professional (in any branch) that touch the day,
/// and nothing is saved here.
/// The free time is the branch hours ∩ the professional's working hours, minus breaks, time off
/// and the appointments that still occupy the agenda.
/// </summary>
public static class AvailabilityCalculator
{
    /// <summary>
    /// The periods (UTC) in which the professional is free on the local date of the branch.
    /// Pass <paramref name="ignoredAppointmentId"/> to leave one appointment out, for example the one being moved.
    /// </summary>
    public static IReadOnlyList<DateTimeRange> GetFreePeriods(
        Branch branch,
        Professional professional,
        DateOnly localDate,
        IEnumerable<Appointment> appointments,
        Guid? ignoredAppointmentId = null)
    {
        var localPeriods = GetLocalWorkingPeriods(branch, professional, localDate);

        if (localPeriods.Count == 0)
        {
            return [];
        }

        var free = localPeriods
            .Select(period => DateTimeRange.Create(
                branch.ToUtc(localDate, period.Start),
                branch.ToUtc(localDate, period.End)))
            .ToList();

        // An appointment in any branch blocks the professional (RN01), and so does time off.
        var blocks = professional.TimeOffs
            .Select(timeOff => timeOff.Range)
            .Concat(appointments
                .Where(appointment => appointment.ProfessionalId == professional.Id
                                      && appointment.OccupiesSchedule
                                      && appointment.Id != ignoredAppointmentId)
                .Select(appointment => appointment.Period));

        foreach (var block in blocks)
        {
            free = free.SelectMany(period => period.Subtract(block)).ToList();
        }

        return free;
    }

    /// <summary>
    /// The start instants (UTC) that can be offered for an appointment whose period lasts <paramref name="length"/>
    /// (the services plus the interval of the policy). Starts are multiples of <paramref name="step"/> on the local
    /// clock of the branch, and respect the minimum notice and the booking window of the policy (RN03).
    /// </summary>
    public static IReadOnlyList<DateTime> GetSlots(
        Branch branch,
        Professional professional,
        DateOnly localDate,
        IEnumerable<Appointment> appointments,
        TimeSpan length,
        TimeSpan step,
        BookingPolicy policy,
        DateTime nowUtc,
        Guid? ignoredAppointmentId = null)
    {
        var validLength = Guard.DurationInWholeMinutes(length, "Availability.LengthInvalid");
        var validStep = Guard.DurationInWholeMinutes(step, "Availability.StepInvalid");

        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("Availability.TimeMustBeUtc");
        }

        var freePeriods = GetFreePeriods(branch, professional, localDate, appointments, ignoredAppointmentId);

        if (freePeriods.Count == 0)
        {
            return [];
        }

        var earliest = nowUtc.AddHours(policy.MinimumNoticeHours);
        var latest = nowUtc.AddDays(policy.MaximumWindowDays);
        var slots = new List<DateTime>();

        foreach (var period in GetLocalWorkingPeriods(branch, professional, localDate))
        {
            var candidate = RoundUp(period.Start.ToTimeSpan(), validStep);
            var end = period.End.ToTimeSpan();

            for (; candidate < end; candidate += validStep)
            {
                var startUtc = branch.ToUtc(localDate, TimeOnly.FromTimeSpan(candidate));

                if (startUtc < earliest || startUtc > latest)
                {
                    continue;
                }

                var range = DateTimeRange.FromDuration(startUtc, validLength);

                if (freePeriods.Any(free => free.Contains(range)))
                {
                    slots.Add(startUtc);
                }
            }
        }

        return slots;
    }

    /// <summary>Whether the whole period fits in the free time of the professional on the day it starts.</summary>
    public static bool IsAvailable(
        Branch branch,
        Professional professional,
        DateTimeRange period,
        IEnumerable<Appointment> appointments,
        Guid? ignoredAppointmentId = null)
    {
        var localDate = branch.GetLocalDate(period.Start);

        return GetFreePeriods(branch, professional, localDate, appointments, ignoredAppointmentId)
            .Any(free => free.Contains(period));
    }

    /// <summary>Same as <see cref="IsAvailable"/>, but throws when the period is not free.</summary>
    public static void EnsureAvailable(
        Branch branch,
        Professional professional,
        DateTimeRange period,
        IEnumerable<Appointment> appointments,
        Guid? ignoredAppointmentId = null)
    {
        if (!IsAvailable(branch, professional, period, appointments, ignoredAppointmentId))
        {
            throw new DomainException("Availability.PeriodNotAvailable");
        }
    }

    // The branch periods (hours minus breaks) crossed with the professional's (hours minus breaks), in local time.
    private static IReadOnlyList<TimeOfDayRange> GetLocalWorkingPeriods(
        Branch branch,
        Professional professional,
        DateOnly localDate)
    {
        if (branch.CompanyId != professional.CompanyId)
        {
            throw new DomainException("Availability.CompanyMismatch");
        }

        if (!branch.IsActive || !professional.IsActive)
        {
            return [];
        }

        var day = localDate.DayOfWeek;
        var branchHours = branch.GetBusinessHours(day);
        var professionalHours = professional.GetWorkingHours(branch.Id, day);

        if (branchHours is null || professionalHours is null)
        {
            return [];
        }

        return branchHours.GetWorkingPeriods()
            .SelectMany(
                branchPeriod => professionalHours.GetWorkingPeriods(),
                (branchPeriod, professionalPeriod) => branchPeriod.Intersect(professionalPeriod))
            .OfType<TimeOfDayRange>()
            .OrderBy(period => period.Start)
            .ToList();
    }

    // The first multiple of the step that is not before the time (counting from midnight).
    private static TimeSpan RoundUp(TimeSpan time, TimeSpan step)
    {
        var steps = (time.Ticks + step.Ticks - 1) / step.Ticks;

        return TimeSpan.FromTicks(steps * step.Ticks);
    }
}