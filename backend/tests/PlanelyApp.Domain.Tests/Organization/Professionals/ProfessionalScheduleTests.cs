using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Organization.Professionals;

public class ProfessionalScheduleTests
{
    private static readonly Guid BranchA = Guid.CreateVersion7();
    private static readonly Guid BranchB = Guid.CreateVersion7();
    private static readonly Guid ServiceA = Guid.CreateVersion7();
    private static readonly Guid ServiceB = Guid.CreateVersion7();

    private static Professional CreateProfessional() =>
        Professional.Create(Guid.CreateVersion7(), "Rafael");

    private static DateTimeRange Range(int day, int startHour, int endHour) =>
        DateTimeRange.Create(
            new DateTime(2026, 10, day, startHour, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, day, endHour, 0, 0, DateTimeKind.Utc));

    private static DailySchedule Schedule(DayOfWeek day, int startHour, int endHour) =>
        DailySchedule.Create(
            day,
            TimeOfDayRange.Create(new TimeOnly(startHour, 0), new TimeOnly(endHour, 0)));

    private static void ShouldChangeVersionBy(Professional professional, Action<Professional> action, int expectedIncrease)
    {
        var before = professional.ScheduleVersion;

        action(professional);

        professional.ScheduleVersion.ShouldBe(before + expectedIncrease);
    }

    // ---- Time off ----

    [Fact]
    public void AddTimeOff_should_store_the_period_and_return_it()
    {
        var professional = CreateProfessional();

        var timeOff = professional.AddTimeOff(Range(20, 9, 12), "  Vacation ");

        timeOff.Id.Version.ShouldBe(7);
        timeOff.Range.ShouldBe(Range(20, 9, 12));
        timeOff.Reason.ShouldBe("Vacation");
        professional.TimeOffs.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_reason_should_become_null(string? reason)
    {
        CreateProfessional().AddTimeOff(Range(20, 9, 12), reason).Reason.ShouldBeNull();
    }

    [Fact]
    public void Reason_above_the_limit_should_be_rejected_and_change_nothing()
    {
        var professional = CreateProfessional();

        Should.Throw<DomainException>(() => professional.AddTimeOff(Range(20, 9, 12), new string('a', 201)))
            .Code.ShouldBe("TimeOff.ReasonInvalid");

        professional.TimeOffs.ShouldBeEmpty();
        professional.ScheduleVersion.ShouldBe(0);
    }

    [Fact]
    public void Time_offs_should_be_ordered_by_start()
    {
        var professional = CreateProfessional();

        professional.AddTimeOff(Range(22, 9, 12));
        professional.AddTimeOff(Range(20, 9, 12));
        professional.AddTimeOff(Range(21, 9, 12));

        professional.TimeOffs.Select(t => t.Range.Start.Day).ToArray().ShouldBe(new[] { 20, 21, 22 });
    }

    [Fact]
    public void Overlapping_time_offs_should_be_accepted()
    {
        var professional = CreateProfessional();

        professional.AddTimeOff(Range(20, 9, 12));
        professional.AddTimeOff(Range(20, 11, 14));

        professional.TimeOffs.Count.ShouldBe(2);
    }

    [Fact]
    public void RemoveTimeOff_should_remove_only_that_period()
    {
        var professional = CreateProfessional();
        var first = professional.AddTimeOff(Range(20, 9, 12));
        var second = professional.AddTimeOff(Range(21, 9, 12));

        professional.RemoveTimeOff(first.Id);

        professional.TimeOffs.Single().Id.ShouldBe(second.Id);
    }

    [Fact]
    public void RemoveTimeOff_of_an_unknown_id_should_be_rejected()
    {
        var professional = CreateProfessional();
        professional.AddTimeOff(Range(20, 9, 12));
        var versionBefore = professional.ScheduleVersion;

        Should.Throw<DomainException>(() => professional.RemoveTimeOff(Guid.CreateVersion7()))
            .Code.ShouldBe("Professional.TimeOffNotFound");

        professional.ScheduleVersion.ShouldBe(versionBefore);
    }

    [Fact]
    public void HasTimeOffDuring_should_detect_overlap_but_not_touching_periods()
    {
        var professional = CreateProfessional();
        professional.AddTimeOff(Range(20, 9, 12));

        professional.HasTimeOffDuring(Range(20, 11, 13)).ShouldBeTrue();
        professional.HasTimeOffDuring(Range(20, 10, 11)).ShouldBeTrue();
        professional.HasTimeOffDuring(Range(20, 12, 14)).ShouldBeFalse();
        professional.HasTimeOffDuring(Range(20, 7, 9)).ShouldBeFalse();
        professional.HasTimeOffDuring(Range(21, 9, 12)).ShouldBeFalse();
    }

    // ---- Offerings ----

    [Fact]
    public void EnableOffering_should_make_the_professional_able_to_perform_the_service()
    {
        var professional = CreateProfessional();

        professional.EnableOffering(ServiceA);

        professional.CanPerform(ServiceA).ShouldBeTrue();
        professional.CanPerform(ServiceB).ShouldBeFalse();
        professional.GetOffering(ServiceA)!.CustomDuration.ShouldBeNull();
    }

    [Fact]
    public void EnableOffering_again_should_replace_the_custom_duration()
    {
        var professional = CreateProfessional();
        professional.EnableOffering(ServiceA, TimeSpan.FromMinutes(45));

        professional.EnableOffering(ServiceA, TimeSpan.FromMinutes(90));

        professional.Offerings.Count.ShouldBe(1);
        professional.GetOffering(ServiceA)!.CustomDuration.ShouldBe(TimeSpan.FromMinutes(90));
    }

    [Fact]
    public void EnableOffering_with_invalid_duration_should_be_rejected_and_change_nothing()
    {
        var professional = CreateProfessional();

        Should.Throw<DomainException>(() => professional.EnableOffering(ServiceA, TimeSpan.Zero))
            .Code.ShouldBe("ProfessionalOffering.DurationInvalid");

        professional.Offerings.ShouldBeEmpty();
        professional.ScheduleVersion.ShouldBe(0);
    }

    [Fact]
    public void DisableOffering_should_remove_the_service_and_be_idempotent()
    {
        var professional = CreateProfessional();
        professional.EnableOffering(ServiceA);
        professional.EnableOffering(ServiceB);

        professional.DisableOffering(ServiceA);
        professional.DisableOffering(ServiceA);

        professional.CanPerform(ServiceA).ShouldBeFalse();
        professional.CanPerform(ServiceB).ShouldBeTrue();
    }

    // ---- Schedule version ----

    [Fact]
    public void New_professional_should_start_at_version_zero()
    {
        CreateProfessional().ScheduleVersion.ShouldBe(0);
    }

    [Fact]
    public void Changes_that_affect_availability_should_increment_the_version()
    {
        var professional = CreateProfessional();

        ShouldChangeVersionBy(professional, p => p.SetWorkingHours(BranchA, Schedule(DayOfWeek.Monday, 9, 18)), 1);
        ShouldChangeVersionBy(professional, p => p.RemoveWorkingHours(BranchA, DayOfWeek.Monday), 1);
        ShouldChangeVersionBy(professional, p => p.AddTimeOff(Range(20, 9, 12)), 1);
        ShouldChangeVersionBy(professional, p => p.RemoveTimeOff(p.TimeOffs[0].Id), 1);
        ShouldChangeVersionBy(professional, p => p.EnableOffering(ServiceA), 1);
        ShouldChangeVersionBy(professional, p => p.DisableOffering(ServiceA), 1);
        ShouldChangeVersionBy(professional, p => p.IncrementScheduleVersion(), 1);
    }

    [Fact]
    public void Operations_that_change_nothing_should_not_increment_the_version()
    {
        var professional = CreateProfessional();

        ShouldChangeVersionBy(professional, p => p.RemoveWorkingHours(BranchA, DayOfWeek.Monday), 0);
        ShouldChangeVersionBy(professional, p => p.DisableOffering(ServiceA), 0);
        ShouldChangeVersionBy(professional, p => p.UpdateDetails("Rafael Lima", null), 0);
        ShouldChangeVersionBy(professional, p => p.Deactivate(), 0);
    }

    [Fact]
    public void Rejected_working_hours_should_not_increment_the_version()
    {
        var professional = CreateProfessional();
        professional.SetWorkingHours(BranchA, Schedule(DayOfWeek.Monday, 9, 13));
        var versionBefore = professional.ScheduleVersion;

        Should.Throw<DomainException>(() =>
            professional.SetWorkingHours(BranchB, Schedule(DayOfWeek.Monday, 12, 18)));

        professional.ScheduleVersion.ShouldBe(versionBefore);
    }
}