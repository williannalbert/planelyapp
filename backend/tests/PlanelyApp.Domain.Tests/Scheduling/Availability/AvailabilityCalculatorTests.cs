using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.Scheduling.Availability;
using PlanelyApp.Domain.Scheduling.Pricing;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Scheduling.Availability;

public class AvailabilityCalculatorTests
{
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly Guid StaffId = Guid.CreateVersion7();

    // Monday 2026-11-09. The branch is in São Paulo (UTC-3 all year), so local 10:00 is 13:00 UTC.
    private static readonly DateOnly Monday = new(2026, 11, 9);
    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    private static DateTime Local(int hour, int minute = 0) =>
        new(2026, 11, 9, hour + 3, minute, 0, DateTimeKind.Utc);

    private static DateTimeRange Between(DateTime start, DateTime end) => DateTimeRange.Create(start, end);

    private static TimeOnly At(int hour, int minute = 0) => new(hour, minute);

    private static BookingPolicy Policy(int minimumNoticeHours = 2) =>
        BookingPolicy.Create(minimumNoticeHours, 60, 24, 0, 30, false);

    // Branch: Monday 09:00-18:00, lunch 12:00-13:00.
    private static Branch CreateBranch(string timeZoneId = "America/Sao_Paulo", Guid? companyId = null)
    {
        var branch = Branch.Create(
            companyId ?? CompanyId,
            "Centro",
            Address.Create("Rua das Flores", "123", null, "Centro", "São Paulo", "SP", "01310-100"),
            Phone.Create("+551133334444"),
            timeZoneId);

        branch.SetBusinessHours(DailySchedule.Create(
            DayOfWeek.Monday,
            TimeOfDayRange.Create(At(9), At(18)),
            [Break.Create(TimeOfDayRange.Create(At(12), At(13)))]));

        return branch;
    }

    // Professional: Monday 10:00-17:00, break 12:30-13:30. With the branch above, the free local time
    // is 10:00-12:00 and 13:30-17:00.
    private static Professional CreateProfessional(Branch branch)
    {
        var professional = Professional.Create(branch.CompanyId, "Ana");

        professional.SetWorkingHours(branch.Id, DailySchedule.Create(
            DayOfWeek.Monday,
            TimeOfDayRange.Create(At(10), At(17)),
            [Break.Create(TimeOfDayRange.Create(At(12, 30), At(13, 30)))]));

        return professional;
    }

    private static IReadOnlyList<DateTimeRange> BaseFreePeriods() =>
    [
        Between(Local(10), Local(12)),
        Between(Local(13, 30), Local(17))
    ];

    private static Appointment Book(
        Guid branchId,
        Guid professionalId,
        DateTime startUtc,
        int minutes = 60,
        bool requiresConfirmation = false)
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
            BookingPolicy.Create(2, 60, 24, 0, 30, requiresConfirmation),
            Now,
            StaffId);
    }

    // ---- Free periods: hours and breaks (RN02) ----

    [Fact]
    public void Free_time_should_be_the_branch_hours_and_the_working_hours_without_the_breaks_of_both()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);

        var free = FreePeriods(branch, professional, Monday, []);

        free.ShouldBe(BaseFreePeriods());
    }

    [Fact]
    public void Free_time_should_follow_the_time_zone_of_the_branch()
    {
        var branch = CreateBranch("America/Manaus"); // UTC-4
        var professional = CreateProfessional(branch);

        var free = FreePeriods(branch, professional, Monday, []);

        free.ShouldBe([
            Between(new DateTime(2026, 11, 9, 14, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 9, 16, 0, 0, DateTimeKind.Utc)),
            Between(new DateTime(2026, 11, 9, 17, 30, 0, DateTimeKind.Utc), new DateTime(2026, 11, 9, 21, 0, 0, DateTimeKind.Utc))]);
    }

    [Fact]
    public void A_day_the_branch_is_closed_should_have_no_free_time()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);

        FreePeriods(branch, professional, new DateOnly(2026, 11, 8), []).ShouldBeEmpty();
    }

    [Fact]
    public void A_professional_who_does_not_work_at_the_branch_that_day_should_have_no_free_time()
    {
        var branch = CreateBranch();
        var professional = Professional.Create(CompanyId, "Ana");
        professional.SetWorkingHours(Guid.CreateVersion7(), DailySchedule.Create(
            DayOfWeek.Monday, TimeOfDayRange.Create(At(10), At(17))));

        FreePeriods(branch, professional, Monday, []).ShouldBeEmpty();
    }

    [Fact]
    public void An_inactive_branch_or_professional_should_have_no_free_time()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);

        branch.Deactivate();
        FreePeriods(branch, professional, Monday, []).ShouldBeEmpty();

        branch.Activate();
        professional.Deactivate();
        FreePeriods(branch, professional, Monday, []).ShouldBeEmpty();
    }

    [Fact]
    public void A_branch_and_a_professional_of_different_companies_should_be_rejected()
    {
        var branch = CreateBranch();
        var stranger = Professional.Create(Guid.CreateVersion7(), "Bia");

        Should.Throw<DomainException>(() => FreePeriods(branch, stranger, Monday, []))
            .Code.ShouldBe("Availability.CompanyMismatch");
    }

    // ---- Free periods: time off ----

    [Fact]
    public void Time_off_should_be_taken_out_of_the_free_time()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);
        professional.AddTimeOff(Between(Local(14), Local(15)));

        var free = FreePeriods(branch, professional, Monday, []);

        free.ShouldBe([
            Between(Local(10), Local(12)),
            Between(Local(13, 30), Local(14)),
            Between(Local(15), Local(17))]);
    }

    [Fact]
    public void Time_off_covering_the_whole_day_should_leave_no_free_time()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);
        professional.AddTimeOff(Between(
            new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc)));

        FreePeriods(branch, professional, Monday, []).ShouldBeEmpty();
    }

    // ---- Free periods: appointments (RN01) ----

    [Fact]
    public void An_appointment_should_be_taken_out_of_the_free_time()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);
        var appointment = Book(branch.Id, professional.Id, Local(10));

        var free = FreePeriods(branch, professional, Monday, [appointment]);

        free.ShouldBe([Between(Local(11), Local(12)), Between(Local(13, 30), Local(17))]);
    }

    [Fact]
    public void An_appointment_in_the_middle_of_the_afternoon_should_split_the_free_time()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);
        var appointment = Book(branch.Id, professional.Id, Local(14));

        var free = FreePeriods(branch, professional, Monday, [appointment]);

        free.ShouldBe([
            Between(Local(10), Local(12)),
            Between(Local(13, 30), Local(14)),
            Between(Local(15), Local(17))]);
    }

    [Fact]
    public void An_appointment_running_into_the_lunch_should_only_take_the_working_part()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);
        var appointment = Book(branch.Id, professional.Id, Local(11, 30));

        var free = FreePeriods(branch, professional, Monday, [appointment]);

        free.ShouldBe([Between(Local(10), Local(11, 30)), Between(Local(13, 30), Local(17))]);
    }

    [Fact]
    public void A_pending_and_an_in_progress_appointment_should_both_block()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);
        var pending = Book(branch.Id, professional.Id, Local(10), requiresConfirmation: true);
        var inProgress = Book(branch.Id, professional.Id, Local(14));
        inProgress.Start(StaffId, Local(14));

        var free = FreePeriods(branch, professional, Monday, [pending, inProgress]);

        free.ShouldBe([
            Between(Local(11), Local(12)),
            Between(Local(13, 30), Local(14)),
            Between(Local(15), Local(17))]);
    }

    [Fact]
    public void Appointments_that_no_longer_occupy_the_agenda_should_not_block()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);

        var cancelled = Book(branch.Id, professional.Id, Local(10));
        cancelled.CancelByStaff(StaffId, Now);

        var noShow = Book(branch.Id, professional.Id, Local(10));
        noShow.MarkAsNoShow(StaffId, Local(10, 5));

        var completed = Book(branch.Id, professional.Id, Local(10));
        completed.Start(StaffId, Local(10));
        completed.Complete(StaffId, Local(11));

        var free = FreePeriods(
            branch, professional, Monday, [cancelled, noShow, completed]);

        free.ShouldBe(BaseFreePeriods());
    }

    [Fact]
    public void An_appointment_of_another_professional_should_not_block()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);
        var other = Book(branch.Id, Guid.CreateVersion7(), Local(10));

        FreePeriods(branch, professional, Monday, [other]).ShouldBe(BaseFreePeriods());
    }

    [Fact]
    public void An_appointment_at_another_branch_should_block_the_professional()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);
        var elsewhere = Book(Guid.CreateVersion7(), professional.Id, Local(10));

        var free = FreePeriods(branch, professional, Monday, [elsewhere]);

        free.ShouldBe([Between(Local(11), Local(12)), Between(Local(13, 30), Local(17))]);
    }

    [Fact]
    public void The_ignored_appointment_should_not_block()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);
        var appointment = Book(branch.Id, professional.Id, Local(10));

        var free = FreePeriods(
            branch, professional, Monday, [appointment], appointment.Id);

        free.ShouldBe(BaseFreePeriods());
    }

    // ---- Checking a period ----

    [Fact]
    public void A_period_inside_the_free_time_should_be_available()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);

        IsAvailable(branch, professional, Between(Local(10), Local(11)), []).ShouldBeTrue();
        IsAvailable(branch, professional, Between(Local(10), Local(12)), []).ShouldBeTrue();
    }

    [Fact]
    public void A_period_across_the_lunch_or_outside_the_hours_should_not_be_available()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);

        IsAvailable(branch, professional, Between(Local(11, 30), Local(12, 30)), []).ShouldBeFalse();
        IsAvailable(branch, professional, Between(Local(12), Local(12, 30)), []).ShouldBeFalse();
        IsAvailable(branch, professional, Between(Local(9, 30), Local(10, 30)), []).ShouldBeFalse();
        IsAvailable(branch, professional, Between(Local(16, 30), Local(17, 30)), []).ShouldBeFalse();
    }

    [Fact]
    public void A_period_that_only_touches_an_appointment_should_be_available()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);
        var appointment = Book(branch.Id, professional.Id, Local(10));

        IsAvailable(
            branch, professional, Between(Local(11), Local(11, 30)), [appointment]).ShouldBeTrue();
    }

    [Fact]
    public void A_period_overlapping_an_appointment_should_not_be_available()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);
        var appointment = Book(branch.Id, professional.Id, Local(10));

        IsAvailable(
            branch, professional, Between(Local(10, 30), Local(11, 30)), [appointment]).ShouldBeFalse();
    }

    [Fact]
    public void The_own_appointment_should_be_ignored_when_it_is_moved()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);
        var appointment = Book(branch.Id, professional.Id, Local(10));
        var newPeriod = Between(Local(10, 30), Local(11, 30));

        IsAvailable(branch, professional, newPeriod, [appointment]).ShouldBeFalse();
        IsAvailable(branch, professional, newPeriod, [appointment], appointment.Id).ShouldBeTrue();
    }

    [Fact]
    public void EnsureAvailable_should_pass_when_free_and_throw_when_not()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);

        EnsureAvailable(branch, professional, Between(Local(10), Local(11)), []);

        Should.Throw<DomainException>(() => EnsureAvailable(
                branch, professional, Between(Local(12), Local(13)), []))
            .Code.ShouldBe("Availability.PeriodNotAvailable");
    }

    // ---- Slots ----

    [Fact]
    public void Slots_should_list_the_starts_that_fit_in_the_free_time()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);

        var slots = AvailabilityCalculator.GetSlots(
            branch, professional, Monday, [], TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(30), Policy(), Now);

        // 10:00-12:00 holds 10:00, 10:30 and 11:00; 13:30-17:00 holds 13:30 to 16:00.
        slots.ShouldBe([
            Local(10), Local(10, 30), Local(11),
            Local(13, 30), Local(14), Local(14, 30), Local(15), Local(15, 30), Local(16)]);
    }

    [Fact]
    public void Slots_should_not_overlap_an_appointment()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);
        var appointment = Book(branch.Id, professional.Id, Local(11));

        var slots = AvailabilityCalculator.GetSlots(
            branch, professional, Monday, [appointment],
            TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(30), Policy(), Now);

        slots.ShouldContain(Local(10));
        slots.ShouldNotContain(Local(10, 30));
        slots.ShouldNotContain(Local(11));
    }

    [Fact]
    public void Slots_should_follow_the_clock_and_not_the_end_of_the_previous_appointment()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);
        var appointment = Book(branch.Id, professional.Id, Local(10), minutes: 20); // ends 10:20

        var slots = AvailabilityCalculator.GetSlots(
            branch, professional, Monday, [appointment],
            TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15), Policy(), Now);

        slots.First().ShouldBe(Local(10, 30));
        slots.ShouldNotContain(Local(10, 20));
    }

    [Fact]
    public void Slots_should_respect_the_minimum_notice()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);
        var morningOfTheDay = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc); // 09:00 local

        var slots = AvailabilityCalculator.GetSlots(
            branch, professional, Monday, [],
            TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(30), Policy(minimumNoticeHours: 2), morningOfTheDay);

        // The earliest start is 14:00 UTC (11:00 local).
        slots.ShouldNotContain(Local(10, 30));
        slots.First().ShouldBe(Local(11));
    }

    [Fact]
    public void Slots_should_respect_the_booking_window()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);
        var farBefore = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc); // the window is 60 days

        AvailabilityCalculator.GetSlots(
                branch, professional, Monday, [],
                TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(30), Policy(), farBefore)
            .ShouldBeEmpty();
    }

    [Fact]
    public void Slots_should_offer_the_own_time_again_when_the_appointment_is_ignored()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);
        var appointment = Book(branch.Id, professional.Id, Local(10));

        AvailabilityCalculator.GetSlots(
                branch, professional, Monday, [appointment],
                TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(30), Policy(), Now)
            .ShouldNotContain(Local(10));

        AvailabilityCalculator.GetSlots(
                branch, professional, Monday, [appointment],
                TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(30), Policy(), Now, appointment.Id)
            .ShouldContain(Local(10));
    }

    [Fact]
    public void Slots_should_reject_invalid_arguments()
    {
        var branch = CreateBranch();
        var professional = CreateProfessional(branch);

        Should.Throw<DomainException>(() => AvailabilityCalculator.GetSlots(
                branch, professional, Monday, [], TimeSpan.Zero, TimeSpan.FromMinutes(30), Policy(), Now))
            .Code.ShouldBe("Availability.LengthInvalid");

        Should.Throw<DomainException>(() => AvailabilityCalculator.GetSlots(
                branch, professional, Monday, [], TimeSpan.FromMinutes(60), TimeSpan.FromSeconds(30), Policy(), Now))
            .Code.ShouldBe("Availability.StepInvalid");

        Should.Throw<DomainException>(() => AvailabilityCalculator.GetSlots(
                branch, professional, Monday, [], TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(30), Policy(),
                new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Unspecified)))
            .Code.ShouldBe("Availability.TimeMustBeUtc");
    }

    private static BookingPolicy NoTravelPolicy() => BookingPolicy.Create(2, 60, 24, 0, 0, false);

    private static IReadOnlyList<DateTimeRange> FreePeriods(
        Branch branch,
        Professional professional,
        DateOnly date,
        IEnumerable<Appointment> appointments,
        Guid? ignoredAppointmentId = null) =>
        AvailabilityCalculator.GetFreePeriods(
            branch, professional, date, appointments, NoTravelPolicy(), ignoredAppointmentId);

    private static bool IsAvailable(
        Branch branch,
        Professional professional,
        DateTimeRange period,
        IEnumerable<Appointment> appointments,
        Guid? ignoredAppointmentId = null) =>
        AvailabilityCalculator.IsAvailable(
            branch, professional, period, appointments, NoTravelPolicy(), ignoredAppointmentId);

    private static void EnsureAvailable(
        Branch branch,
        Professional professional,
        DateTimeRange period,
        IEnumerable<Appointment> appointments,
        Guid? ignoredAppointmentId = null) =>
        AvailabilityCalculator.EnsureAvailable(
            branch, professional, period, appointments, NoTravelPolicy(), ignoredAppointmentId);
}