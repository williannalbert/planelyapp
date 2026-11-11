using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Branches.RemoveBusinessHours;
using PlanelyApp.Application.Organization.Branches.SetBusinessHours;
using PlanelyApp.Application.Organization.Shared;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests.Organization.Branches;

public class BusinessHoursTests
{
    private static readonly BreakData Lunch = new(new TimeOnly(12, 0), new TimeOnly(13, 0), "Lunch");

    private static DailyScheduleData Monday(params BreakData[] breaks) =>
        new(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(18, 0), breaks);

    [Fact]
    public async Task A_weekday_should_be_opened_with_its_breaks()
    {
        var branch = BranchData.NewBranch();
        var handler = new SetBranchBusinessHoursHandler(BranchData.RepositoryWith(branch));

        var result = await handler.HandleAsync(
            new SetBranchBusinessHoursCommand(branch.Id, Monday(Lunch)), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var hours = branch.GetBusinessHours(DayOfWeek.Monday);
        hours.ShouldNotBeNull();
        hours.Range.Start.ShouldBe(new TimeOnly(9, 0));
        hours.Range.End.ShouldBe(new TimeOnly(18, 0));
        var pause = hours.Breaks.ShouldHaveSingleItem();
        pause.Range.Start.ShouldBe(new TimeOnly(12, 0));
        pause.Description.ShouldBe("Lunch");
    }

    [Fact]
    public async Task Setting_a_weekday_again_should_replace_its_hours()
    {
        var branch = BranchData.NewBranch();
        var handler = new SetBranchBusinessHoursHandler(BranchData.RepositoryWith(branch));
        var cancellationToken = TestContext.Current.CancellationToken;

        await handler.HandleAsync(new SetBranchBusinessHoursCommand(branch.Id, Monday(Lunch)), cancellationToken);
        await handler.HandleAsync(
            new SetBranchBusinessHoursCommand(
                branch.Id, new DailyScheduleData(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0), [])),
            cancellationToken);

        var hours = branch.BusinessHours.ShouldHaveSingleItem();
        hours.Range.End.ShouldBe(new TimeOnly(12, 0));
        hours.Breaks.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_break_outside_the_hours_should_be_refused_by_the_domain()
    {
        var branch = BranchData.NewBranch();
        var handler = new SetBranchBusinessHoursHandler(BranchData.RepositoryWith(branch));
        var late = new BreakData(new TimeOnly(17, 30), new TimeOnly(19, 0), null);

        var exception = await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(
            new SetBranchBusinessHoursCommand(branch.Id, Monday(late)), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("DailySchedule.BreakOutsideRange");
        branch.BusinessHours.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_closing_time_before_the_opening_should_be_refused_by_the_domain()
    {
        var branch = BranchData.NewBranch();
        var handler = new SetBranchBusinessHoursHandler(BranchData.RepositoryWith(branch));
        var backwards = new DailyScheduleData(DayOfWeek.Monday, new TimeOnly(18, 0), new TimeOnly(9, 0), []);

        var exception = await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(
            new SetBranchBusinessHoursCommand(branch.Id, backwards), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("TimeOfDayRange.EndMustBeAfterStart");
    }

    [Fact]
    public async Task Hours_for_a_missing_branch_should_be_not_found()
    {
        var handler = new SetBranchBusinessHoursHandler(BranchData.RepositoryWith(null));

        var result = await handler.HandleAsync(
            new SetBranchBusinessHoursCommand(Guid.NewGuid(), Monday()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Branch.NotFound");
    }

    [Fact]
    public async Task The_validator_should_require_the_schedule_and_its_list_of_breaks()
    {
        var validator = new SetBranchBusinessHoursValidator();

        var withoutSchedule = await TestValidation.RunAsync(
            validator, new SetBranchBusinessHoursCommand(Guid.NewGuid(), null!));
        var withoutBreaks = await TestValidation.RunAsync(
            validator,
            new SetBranchBusinessHoursCommand(
                Guid.NewGuid(), new DailyScheduleData(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(18, 0), null!)));

        withoutSchedule.ShouldBe(["Schedule:Validation.Required"], ignoreOrder: true);
        withoutBreaks.ShouldBe(["Schedule.Breaks:Validation.Required"], ignoreOrder: true);
    }

    [Fact]
    public async Task Closing_a_weekday_should_remove_its_hours()
    {
        var branch = BranchData.NewBranch();
        branch.SetBusinessHours(Monday(Lunch).ToDomain());
        var handler = new RemoveBranchBusinessHoursHandler(BranchData.RepositoryWith(branch));

        var result = await handler.HandleAsync(
            new RemoveBranchBusinessHoursCommand(branch.Id, DayOfWeek.Monday), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        branch.GetBusinessHours(DayOfWeek.Monday).ShouldBeNull();
    }

    [Fact]
    public async Task Closing_a_weekday_of_a_missing_branch_should_be_not_found()
    {
        var handler = new RemoveBranchBusinessHoursHandler(BranchData.RepositoryWith(null));

        var result = await handler.HandleAsync(
            new RemoveBranchBusinessHoursCommand(Guid.NewGuid(), DayOfWeek.Monday),
            TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Branch.NotFound");
    }
}