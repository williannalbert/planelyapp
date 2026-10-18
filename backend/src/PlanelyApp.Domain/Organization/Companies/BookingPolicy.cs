using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.Organization.Companies;

/// <summary>Company-wide rules that limit when and how appointments can be made.</summary>
public sealed class BookingPolicy : ValueObject
{
    public static readonly BookingPolicy Default = new(
        minimumNoticeHours: 2,
        maximumWindowDays: 60,
        cancellationDeadlineHours: 24,
        intervalBetweenAppointmentsMinutes: 0,
        travelTimeBetweenBranchesMinutes: 30,
        requiresConfirmation: false);

    private BookingPolicy(
        int minimumNoticeHours,
        int maximumWindowDays,
        int cancellationDeadlineHours,
        int intervalBetweenAppointmentsMinutes,
        int travelTimeBetweenBranchesMinutes,
        bool requiresConfirmation)
    {
        MinimumNoticeHours = minimumNoticeHours;
        MaximumWindowDays = maximumWindowDays;
        CancellationDeadlineHours = cancellationDeadlineHours;
        IntervalBetweenAppointmentsMinutes = intervalBetweenAppointmentsMinutes;
        TravelTimeBetweenBranchesMinutes = travelTimeBetweenBranchesMinutes;
        RequiresConfirmation = requiresConfirmation;
    }

    /// <summary>How far ahead of the appointment it must be booked (RN03).</summary>
    public int MinimumNoticeHours { get; }

    /// <summary>How many days ahead an appointment can be booked (RN03).</summary>
    public int MaximumWindowDays { get; }

    /// <summary>Until when before the start the customer can cancel or reschedule (RN06).</summary>
    public int CancellationDeadlineHours { get; }

    /// <summary>Free time added after each appointment of the same professional (RN05).</summary>
    public int IntervalBetweenAppointmentsMinutes { get; }

    /// <summary>Minimum time between appointments of a professional in different branches (RN12).</summary>
    public int TravelTimeBetweenBranchesMinutes { get; }

    /// <summary>When true, new appointments wait for staff confirmation.</summary>
    public bool RequiresConfirmation { get; }

    public static BookingPolicy Create(
        int minimumNoticeHours,
        int maximumWindowDays,
        int cancellationDeadlineHours,
        int intervalBetweenAppointmentsMinutes,
        int travelTimeBetweenBranchesMinutes,
        bool requiresConfirmation)
    {
        if (minimumNoticeHours < 0)
        {
            throw new DomainException("BookingPolicy.MinimumNoticeInvalid");
        }

        if (maximumWindowDays < 1)
        {
            throw new DomainException("BookingPolicy.MaximumWindowInvalid");
        }

        if (cancellationDeadlineHours < 0)
        {
            throw new DomainException("BookingPolicy.CancellationDeadlineInvalid");
        }

        if (intervalBetweenAppointmentsMinutes < 0)
        {
            throw new DomainException("BookingPolicy.IntervalInvalid");
        }

        if (travelTimeBetweenBranchesMinutes < 0)
        {
            throw new DomainException("BookingPolicy.TravelTimeInvalid");
        }

        // Otherwise no instant could ever be booked.
        if ((long)maximumWindowDays * 24 <= minimumNoticeHours)
        {
            throw new DomainException("BookingPolicy.WindowSmallerThanNotice");
        }

        return new BookingPolicy(
            minimumNoticeHours,
            maximumWindowDays,
            cancellationDeadlineHours,
            intervalBetweenAppointmentsMinutes,
            travelTimeBetweenBranchesMinutes,
            requiresConfirmation);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MinimumNoticeHours;
        yield return MaximumWindowDays;
        yield return CancellationDeadlineHours;
        yield return IntervalBetweenAppointmentsMinutes;
        yield return TravelTimeBetweenBranchesMinutes;
        yield return RequiresConfirmation;
    }
}