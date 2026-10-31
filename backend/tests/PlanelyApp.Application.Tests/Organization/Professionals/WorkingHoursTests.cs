using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Professionals.RemoveWorkingHours;
using PlanelyApp.Application.Organization.Professionals.SetWorkingHours;
using PlanelyApp.Application.Organization.Shared;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.SeedWork;
using Shouldly;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;

namespace PlanelyApp.Application.Tests.Organization.Professionals;

public class WorkingHoursTests
{
    private static SetProfessionalWorkingHoursHandler Handler(Professional? professional, Branch? branch) =>
        new(ProfessionalData.RepositoryWith(professional), BranchData.RepositoryWith(branch));

    private static SetProfessionalWorkingHoursCommand Set(
        Professional professional,
        Branch branch,
        DailyScheduleData schedule) => new(professional.Id, branch.Id, schedule);

    [Fact]
    public async Task A_weekday_at_a_branch_should_be_set_and_raise_the_agenda_version()
    {
        var professional = ProfessionalData.NewProfessional();
        var branch = BranchData.NewBranch();
        var lunch = new BreakData(new TimeOnly(12, 0), new TimeOnly(13, 0), "Lunch");

        var result = await Handler(professional, branch).HandleAsync(
            Set(professional, branch, ProfessionalData.Day(DayOfWeek.Monday, 9, 18, lunch)),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var hours = professional.GetWorkingHours(branch.Id, DayOfWeek.Monday);
        hours.ShouldNotBeNull();
        hours.Range.Start.ShouldBe(new TimeOnly(9, 0));
        hours.Breaks.ShouldHaveSingleItem().Description.ShouldBe("Lunch");
        professional.ScheduleVersion.ShouldBe(1);
    }

    [Fact]
    public async Task Setting_the_same_weekday_at_the_same_branch_again_should_replace_it()
    {
        var professional = ProfessionalData.NewProfessional();
        var branch = BranchData.NewBranch();
        var handler = Handler(professional, branch);
        var cancellationToken = TestContext.Current.CancellationToken;

        await handler.HandleAsync(Set(professional, branch, ProfessionalData.Day(DayOfWeek.Monday, 9, 18)), cancellationToken);
        await handler.HandleAsync(Set(professional, branch, ProfessionalData.Day(DayOfWeek.Monday, 8, 12)), cancellationToken);

        professional.WorkingHours.ShouldHaveSingleItem().Schedule.Range.End.ShouldBe(new TimeOnly(12, 0));
        professional.ScheduleVersion.ShouldBe(2);
    }

    [Fact]
    public async Task Overlapping_hours_at_another_branch_should_be_refused_by_the_domain()
    {
        var professional = ProfessionalData.NewProfessional();
        var downtown = BranchData.NewBranch(name: "Centro");
        var north = BranchData.NewBranch(name: "Norte");
        var cancellationToken = TestContext.Current.CancellationToken;

        await Handler(professional, downtown).HandleAsync(
            Set(professional, downtown, ProfessionalData.Day(DayOfWeek.Monday, 9, 18)), cancellationToken);

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(professional, north).HandleAsync(
            Set(professional, north, ProfessionalData.Day(DayOfWeek.Monday, 17, 20)), cancellationToken));

        exception.Code.ShouldBe("Professional.WorkingHoursConflict");
        professional.WorkingHours.ShouldHaveSingleItem().BranchId.ShouldBe(downtown.Id);
        professional.ScheduleVersion.ShouldBe(1);
    }

    [Fact]
    public async Task The_same_weekday_at_two_branches_without_overlap_should_be_allowed()
    {
        var professional = ProfessionalData.NewProfessional();
        var downtown = BranchData.NewBranch(name: "Centro");
        var north = BranchData.NewBranch(name: "Norte");
        var cancellationToken = TestContext.Current.CancellationToken;

        await Handler(professional, downtown).HandleAsync(
            Set(professional, downtown, ProfessionalData.Day(DayOfWeek.Monday, 9, 12)), cancellationToken);
        await Handler(professional, north).HandleAsync(
            Set(professional, north, ProfessionalData.Day(DayOfWeek.Monday, 14, 18)), cancellationToken);

        professional.WorkingHours.Count.ShouldBe(2);
        professional.ScheduleVersion.ShouldBe(2);
    }

    [Fact]
    public async Task A_missing_professional_should_be_not_found_without_looking_for_the_branch()
    {
        var branch = BranchData.NewBranch();
        var branches = BranchData.RepositoryWith(branch);
        var handler = new SetProfessionalWorkingHoursHandler(ProfessionalData.RepositoryWith(null), branches);

        var result = await handler.HandleAsync(
            new SetProfessionalWorkingHoursCommand(Guid.NewGuid(), branch.Id, ProfessionalData.Day(DayOfWeek.Monday, 9, 18)),
            TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Professional.NotFound");
        await branches.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_missing_branch_should_be_not_found_and_leave_the_agenda_alone()
    {
        var professional = ProfessionalData.NewProfessional();

        var result = await Handler(professional, null).HandleAsync(
            new SetProfessionalWorkingHoursCommand(
                professional.Id, Guid.NewGuid(), ProfessionalData.Day(DayOfWeek.Monday, 9, 18)),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Branch.NotFound");
        professional.WorkingHours.ShouldBeEmpty();
        professional.ScheduleVersion.ShouldBe(0);
    }

    [Fact]
    public async Task A_break_outside_the_hours_should_be_refused_by_the_domain()
    {
        var professional = ProfessionalData.NewProfessional();
        var branch = BranchData.NewBranch();
        var late = new BreakData(new TimeOnly(17, 30), new TimeOnly(19, 0), null);

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(professional, branch).HandleAsync(
            Set(professional, branch, ProfessionalData.Day(DayOfWeek.Monday, 9, 18, late)),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("DailySchedule.BreakOutsideRange");
        professional.WorkingHours.ShouldBeEmpty();
        professional.ScheduleVersion.ShouldBe(0);
    }

    [Fact]
    public async Task The_validator_should_require_the_branch_and_the_schedule()
    {
        var problems = await TestValidation.RunAsync(
            new SetProfessionalWorkingHoursValidator(),
            new SetProfessionalWorkingHoursCommand(Guid.NewGuid(), Guid.Empty, null!));

        problems.ShouldBe(["BranchId:Validation.Required", "Schedule:Validation.Required"], ignoreOrder: true);
    }

    [Fact]
    public async Task Removing_a_weekday_should_drop_it_and_raise_the_agenda_version()
    {
        var professional = ProfessionalData.NewProfessional();
        var branchId = Guid.NewGuid();
        professional.SetWorkingHours(branchId, ProfessionalData.Day(DayOfWeek.Monday, 9, 18).ToDomain());
        var handler = new RemoveProfessionalWorkingHoursHandler(ProfessionalData.RepositoryWith(professional));

        var result = await handler.HandleAsync(
            new RemoveProfessionalWorkingHoursCommand(professional.Id, branchId, DayOfWeek.Monday),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        professional.GetWorkingHours(branchId, DayOfWeek.Monday).ShouldBeNull();
        professional.ScheduleVersion.ShouldBe(2);
    }

    [Fact]
    public async Task Removing_a_weekday_that_was_not_set_should_change_nothing()
    {
        var professional = ProfessionalData.NewProfessional();
        var handler = new RemoveProfessionalWorkingHoursHandler(ProfessionalData.RepositoryWith(professional));

        var result = await handler.HandleAsync(
            new RemoveProfessionalWorkingHoursCommand(professional.Id, Guid.NewGuid(), DayOfWeek.Tuesday),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        professional.ScheduleVersion.ShouldBe(0);
    }

    [Fact]
    public async Task Removing_hours_of_a_missing_professional_should_be_not_found()
    {
        var handler = new RemoveProfessionalWorkingHoursHandler(ProfessionalData.RepositoryWith(null));

        var result = await handler.HandleAsync(
            new RemoveProfessionalWorkingHoursCommand(Guid.NewGuid(), Guid.NewGuid(), DayOfWeek.Monday),
            TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Professional.NotFound");
    }
}