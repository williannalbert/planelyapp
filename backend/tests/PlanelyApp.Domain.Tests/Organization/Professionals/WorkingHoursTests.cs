using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Organization.Professionals;

public class WorkingHoursTests
{
    private static DailySchedule Schedule() =>
        DailySchedule.Create(
            DayOfWeek.Monday,
            TimeOfDayRange.Create(new TimeOnly(9, 0), new TimeOnly(18, 0)));

    [Fact]
    public void Should_keep_branch_and_schedule()
    {
        var branchId = Guid.CreateVersion7();

        var hours = WorkingHours.Create(branchId, Schedule());

        hours.BranchId.ShouldBe(branchId);
        hours.Schedule.ShouldBe(Schedule());
    }

    [Fact]
    public void Empty_branch_id_should_be_rejected()
    {
        Should.Throw<DomainException>(() => WorkingHours.Create(Guid.Empty, Schedule()))
            .Code.ShouldBe("WorkingHours.BranchRequired");
    }

    [Fact]
    public void Same_branch_and_schedule_should_be_equal()
    {
        var branchId = Guid.CreateVersion7();

        WorkingHours.Create(branchId, Schedule()).ShouldBe(WorkingHours.Create(branchId, Schedule()));
        WorkingHours.Create(branchId, Schedule()).ShouldNotBe(WorkingHours.Create(Guid.CreateVersion7(), Schedule()));
    }
}