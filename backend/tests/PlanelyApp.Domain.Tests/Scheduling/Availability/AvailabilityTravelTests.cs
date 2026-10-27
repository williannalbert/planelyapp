using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.Scheduling.Availability;
using PlanelyApp.Domain.Scheduling.Pricing;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Scheduling.Availability;

public class AvailabilityTravelTests
{
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly Guid StaffId = Guid.CreateVersion7();

    // Monday 2026-11-09 in São Paulo (UTC-3): local 10:00 is 13:00 UTC.
    private static readonly DateOnly Monday = new(2026, 11, 9);
    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    private static DateTime Local(int hour, int minute = 0) =>
        new(2026, 11, 9, hour + 3, minute, 0, DateTimeKind.Utc);

    private static DateTimeRange Between(DateTime start, DateTime end) => DateTimeRange.Create(start, end);

    private static TimeOnly At(int hour, int minute = 0) => new(hour, minute);

    private static BookingPolicy Policy(int travelMinutes) =>
        BookingPolicy.Create(2, 60, 24, 0, travelMinutes, false);

    // Branch and professional both work Monday 10:00-17:00, so the free local time is 10:00-17:00.
    private static (Branch Branch, Professional Professional) Setup()
    {
        var branch = Branch.Create(
            CompanyId,
            "Centro",
            Address.Create("Rua das Flores", "123", null, "Centro", "São Paulo", "SP", "01310-100"),
            Phone.Create("+551133334444"),
            "America/Sao_Paulo");
        var hours = TimeOfDayRange.Create(At(10), At(17));
        branch.SetBusinessHours(DailySchedule.Create(DayOfWeek.Monday, hours));

        var professional = Professional.Create(CompanyId, "Ana");
        professional.SetWorkingHours(branch.Id, DailySchedule.Create(DayOfWeek.Monday, hours));

        return (branch, professional);
    }

    private static Appointment Book(Guid branchId, Guid professionalId, DateTime startUtc, int minutes = 60)
    {
        var service = ServiceOffering.CreateWithFixedPrice(
            CompanyId, "Haircut", TimeSpan.FromMinutes(minutes), Money.From(80m));

        return Appointment.Create(
            CompanyId,
            branchId,
            Guid.CreateVersion7(),
            professionalId,
            startUtc,
            [new NewAppointmentItem(service, TimeSpan.FromMinutes(minutes), PriceQuote.Fixed(Money.From(80m)))],
            BookingPolicy.Create(2, 60, 24, 0, 30, false),
            Now,
            StaffId);
    }

    [Fact]
    public void An_appointment_at_another_branch_should_block_the_travel_time_after_it()
    {
        var (branch, professional) = Setup();
        var elsewhere = Book(Guid.CreateVersion7(), professional.Id, Local(10)); // ends 11:00

        var free = AvailabilityCalculator.GetFreePeriods(branch, professional, Monday, [elsewhere], Policy(30));

        free.ShouldBe([Between(Local(11, 30), Local(17))]);
    }

    [Fact]
    public void An_appointment_at_another_branch_should_block_the_travel_time_before_it()
    {
        var (branch, professional) = Setup();
        var elsewhere = Book(Guid.CreateVersion7(), professional.Id, Local(14)); // 14:00-15:00

        var free = AvailabilityCalculator.GetFreePeriods(branch, professional, Monday, [elsewhere], Policy(30));

        free.ShouldBe([Between(Local(10), Local(13, 30)), Between(Local(15, 30), Local(17))]);
    }

    [Fact]
    public void An_appointment_at_the_same_branch_should_not_block_any_travel_time()
    {
        var (branch, professional) = Setup();
        var here = Book(branch.Id, professional.Id, Local(10));

        var free = AvailabilityCalculator.GetFreePeriods(branch, professional, Monday, [here], Policy(30));

        free.ShouldBe([Between(Local(11), Local(17))]);
    }

    [Fact]
    public void Without_travel_time_in_the_policy_another_branch_should_block_only_the_appointment()
    {
        var (branch, professional) = Setup();
        var elsewhere = Book(Guid.CreateVersion7(), professional.Id, Local(10));

        var free = AvailabilityCalculator.GetFreePeriods(branch, professional, Monday, [elsewhere], Policy(0));

        free.ShouldBe([Between(Local(11), Local(17))]);
    }

    [Fact]
    public void Slots_should_leave_room_for_the_travel_from_another_branch()
    {
        var (branch, professional) = Setup();
        var elsewhere = Book(Guid.CreateVersion7(), professional.Id, Local(10));

        var slots = AvailabilityCalculator.GetSlots(
            branch, professional, Monday, [elsewhere],
            TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30), Policy(30), Now);

        slots.ShouldNotContain(Local(11));
        slots.First().ShouldBe(Local(11, 30));
    }

    [Fact]
    public void A_period_too_close_to_an_appointment_at_another_branch_should_not_be_available()
    {
        var (branch, professional) = Setup();
        var elsewhere = Book(Guid.CreateVersion7(), professional.Id, Local(10));

        AvailabilityCalculator.IsAvailable(
            branch, professional, Between(Local(11), Local(11, 30)), [elsewhere], Policy(30)).ShouldBeFalse();
        AvailabilityCalculator.IsAvailable(
            branch, professional, Between(Local(11, 30), Local(12)), [elsewhere], Policy(30)).ShouldBeTrue();
    }
}