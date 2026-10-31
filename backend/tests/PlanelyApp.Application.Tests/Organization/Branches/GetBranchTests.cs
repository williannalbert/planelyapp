using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Branches.Get;
using PlanelyApp.Application.Organization.Shared;
using Shouldly;

namespace PlanelyApp.Application.Tests.Organization.Branches;

public class GetBranchTests
{
    [Fact]
    public async Task The_branch_should_come_back_as_a_dto_with_its_weekdays_in_order()
    {
        var branch = BranchData.NewBranch();
        var lunch = new BreakData(new TimeOnly(12, 0), new TimeOnly(13, 0), "Lunch");
        branch.SetBusinessHours(new DailyScheduleData(
            DayOfWeek.Wednesday, new TimeOnly(10, 0), new TimeOnly(19, 0), []).ToDomain());
        branch.SetBusinessHours(new DailyScheduleData(
            DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(18, 0), [lunch]).ToDomain());
        var handler = new GetBranchHandler(BranchData.RepositoryWith(branch));

        var result = await handler.HandleAsync(new GetBranchQuery(branch.Id), TestContext.Current.CancellationToken);

        var dto = result.Value;
        dto.Id.ShouldBe(branch.Id);
        dto.Name.ShouldBe("Centro");
        dto.Address.ShouldBe(BranchData.NewAddress());
        dto.Phone.ShouldBe(BranchData.ValidPhone);
        dto.TimeZoneId.ShouldBe(BranchData.ValidTimeZone);
        dto.IsActive.ShouldBeTrue();
        dto.BusinessHours.Select(schedule => schedule.Day)
            .ShouldBe([DayOfWeek.Monday, DayOfWeek.Wednesday], ignoreOrder: false);
        dto.BusinessHours[0].Breaks.ShouldBe([lunch], ignoreOrder: false);
        dto.BusinessHours[1].Breaks.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_branch_without_hours_should_have_an_empty_list()
    {
        var branch = BranchData.NewBranch();
        var handler = new GetBranchHandler(BranchData.RepositoryWith(branch));

        var result = await handler.HandleAsync(new GetBranchQuery(branch.Id), TestContext.Current.CancellationToken);

        result.Value.BusinessHours.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_missing_branch_should_be_not_found()
    {
        var handler = new GetBranchHandler(BranchData.RepositoryWith(null));

        var result = await handler.HandleAsync(
            new GetBranchQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Branch.NotFound");
    }
}