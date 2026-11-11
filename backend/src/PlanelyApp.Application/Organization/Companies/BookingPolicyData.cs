using PlanelyApp.Domain.Organization.Companies;

namespace PlanelyApp.Application.Organization.Companies;

/// <summary>The booking policy as it travels in and out of the Api.</summary>
public sealed record BookingPolicyData(
    int MinimumNoticeHours,
    int MaximumWindowDays,
    int CancellationDeadlineHours,
    int IntervalBetweenAppointmentsMinutes,
    int TravelTimeBetweenBranchesMinutes,
    bool RequiresConfirmation)
{
    internal static BookingPolicyData From(BookingPolicy policy) => new(
        policy.MinimumNoticeHours,
        policy.MaximumWindowDays,
        policy.CancellationDeadlineHours,
        policy.IntervalBetweenAppointmentsMinutes,
        policy.TravelTimeBetweenBranchesMinutes,
        policy.RequiresConfirmation);

    /// <summary>Throws <c>DomainException</c> when a value is out of range: the limits are the Domain's.</summary>
    internal BookingPolicy ToDomain() => BookingPolicy.Create(
        MinimumNoticeHours,
        MaximumWindowDays,
        CancellationDeadlineHours,
        IntervalBetweenAppointmentsMinutes,
        TravelTimeBetweenBranchesMinutes,
        RequiresConfirmation);
}