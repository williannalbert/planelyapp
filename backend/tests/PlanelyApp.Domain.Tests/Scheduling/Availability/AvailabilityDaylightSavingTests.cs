using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.Scheduling.Availability;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Scheduling.Availability;

// New York: the clocks jump from 02:00 to 03:00 on Sunday 2026-03-08 (07:00 UTC)
// and go back from 02:00 to 01:00 on Sunday 2026-11-01 (06:00 UTC).
public class AvailabilityDaylightSavingTests
{
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly DateOnly SpringForward = new(2026, 3, 8);
    private static readonly DateOnly FallBack = new(2026, 11, 1);

    private static DateTime Utc(int month, int day, int hour, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static BookingPolicy Policy() => BookingPolicy.Create(0, 60, 24, 0, 0, false);

    // Branch and professional both work on Sundays during the same hours.
    private static (Branch Branch, Professional Professional) Setup(TimeOnly start, TimeOnly end)
    {
        var branch = Branch.Create(
            CompanyId,
            "Midtown",
            Address.Create("Rua das Flores", "123", null, "Centro", "São Paulo", "SP", "01310-100"),
            Phone.Create("+551133334444"),
            "America/New_York");
        var hours = TimeOfDayRange.Create(start, end);
        branch.SetBusinessHours(DailySchedule.Create(DayOfWeek.Sunday, hours));

        var professional = Professional.Create(CompanyId, "Ana");
        professional.SetWorkingHours(branch.Id, DailySchedule.Create(DayOfWeek.Sunday, hours));

        return (branch, professional);
    }

    [Fact]
    public void The_day_the_clocks_jump_forward_should_have_one_hour_less()
    {
        var (branch, professional) = Setup(new TimeOnly(0, 0), new TimeOnly(6, 0));

        var free = AvailabilityCalculator.GetFreePeriods(branch, professional, SpringForward, [], Policy());

        // 00:00 EST is 05:00 UTC and 06:00 EDT is 10:00 UTC: five real hours.
        free.ShouldBe([DateTimeRange.Create(Utc(3, 8, 5), Utc(3, 8, 10))]);
    }

    [Fact]
    public void The_day_the_clocks_go_back_should_have_one_hour_more()
    {
        var (branch, professional) = Setup(new TimeOnly(0, 0), new TimeOnly(6, 0));

        var free = AvailabilityCalculator.GetFreePeriods(branch, professional, FallBack, [], Policy());

        // 00:00 EDT is 04:00 UTC and 06:00 EST is 11:00 UTC: seven real hours.
        free.ShouldBe([DateTimeRange.Create(Utc(11, 1, 4), Utc(11, 1, 11))]);
    }

    [Fact]
    public void Hours_starting_in_the_skipped_hour_should_start_at_the_jump()
    {
        var (branch, professional) = Setup(new TimeOnly(2, 30), new TimeOnly(4, 0));

        var free = AvailabilityCalculator.GetFreePeriods(branch, professional, SpringForward, [], Policy());

        free.ShouldBe([DateTimeRange.Create(Utc(3, 8, 7), Utc(3, 8, 8))]);
    }

    [Fact]
    public void Hours_entirely_inside_the_skipped_hour_should_leave_no_free_time()
    {
        var (branch, professional) = Setup(new TimeOnly(2, 10), new TimeOnly(2, 50));

        AvailabilityCalculator.GetFreePeriods(branch, professional, SpringForward, [], Policy()).ShouldBeEmpty();
    }

    [Fact]
    public void Slots_should_not_repeat_an_instant_when_the_clocks_jump_forward()
    {
        var (branch, professional) = Setup(new TimeOnly(1, 0), new TimeOnly(4, 0));

        var slots = AvailabilityCalculator.GetSlots(
            branch, professional, SpringForward, [],
            TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(60), Policy(), Utc(3, 1, 12));

        // 01:00 EST, then 02:00 and 03:00 are the same instant (the jump).
        slots.ShouldBe([Utc(3, 8, 6), Utc(3, 8, 7)]);
    }

    [Fact]
    public void Slots_should_offer_a_repeated_local_time_once_at_its_later_occurrence()
    {
        var (branch, professional) = Setup(new TimeOnly(0, 30), new TimeOnly(3, 0));

        var slots = AvailabilityCalculator.GetSlots(
            branch, professional, FallBack, [],
            TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(60), Policy(), Utc(10, 25, 12));

        // 01:00 happens twice (05:00 and 06:00 UTC); only the later one is offered, then 02:00 EST.
        slots.ShouldBe([Utc(11, 1, 6), Utc(11, 1, 7)]);
    }
}