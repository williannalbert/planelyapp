using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Organization.Branches;

public class BranchTests
{
    private static readonly Guid CompanyId = Guid.CreateVersion7();

    private static Address CreateAddress() =>
        Address.Create("Rua das Flores", "123", null, "Centro", "São Paulo", "SP", "01310-100");

    private static Branch CreateBranch(
        Guid? companyId = null,
        string? name = "Centro",
        string? timeZoneId = "America/Sao_Paulo") =>
        Branch.Create(
            companyId ?? CompanyId,
            name,
            CreateAddress(),
            Phone.Create("+551133334444"),
            timeZoneId);

    private static TimeOfDayRange Range(int startHour, int endHour) =>
        TimeOfDayRange.Create(new TimeOnly(startHour, 0), new TimeOnly(endHour, 0));

    private static DailySchedule Schedule(DayOfWeek day, params Break[] breaks) =>
        DailySchedule.Create(day, Range(9, 18), breaks);

    private static Break Lunch() => Break.Create(Range(12, 13), "Lunch");

    [Fact]
    public void Create_should_register_an_active_branch_without_business_hours()
    {
        var branch = CreateBranch(name: "  Centro ");

        branch.Id.Version.ShouldBe(7);
        branch.CompanyId.ShouldBe(CompanyId);
        branch.Name.ShouldBe("Centro");
        branch.Address.City.ShouldBe("São Paulo");
        branch.Phone.Value.ShouldBe("+551133334444");
        branch.TimeZoneId.ShouldBe("America/Sao_Paulo");
        branch.IsActive.ShouldBeTrue();
        branch.BusinessHours.ShouldBeEmpty();
    }

    [Fact]
    public void Branch_should_be_a_tenant_entity()
    {
        ITenantEntity tenantEntity = CreateBranch();

        tenantEntity.CompanyId.ShouldBe(CompanyId);
    }

    [Fact]
    public void Empty_company_id_should_be_rejected()
    {
        Should.Throw<DomainException>(() => CreateBranch(companyId: Guid.Empty))
            .Code.ShouldBe("Branch.CompanyRequired");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_name_should_be_rejected(string? name)
    {
        Should.Throw<DomainException>(() => CreateBranch(name: name))
            .Code.ShouldBe("Branch.NameInvalid");
    }

    [Fact]
    public void Name_above_the_limit_should_be_rejected()
    {
        Should.Throw<DomainException>(() => CreateBranch(name: new string('a', 101)))
            .Code.ShouldBe("Branch.NameInvalid");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Mars/Olympus")]
    public void Invalid_time_zone_should_be_rejected(string? timeZoneId)
    {
        Should.Throw<DomainException>(() => CreateBranch(timeZoneId: timeZoneId))
            .Code.ShouldBe("Branch.TimeZoneInvalid");
    }

    [Fact]
    public void Valid_time_zones_should_be_kept_as_given()
    {
        CreateBranch(timeZoneId: "America/Manaus").TimeZoneId.ShouldBe("America/Manaus");
    }

    [Fact]
    public void SetBusinessHours_should_open_the_weekday()
    {
        var branch = CreateBranch();

        branch.SetBusinessHours(Schedule(DayOfWeek.Monday, Lunch()));

        branch.BusinessHours.Count.ShouldBe(1);
        branch.GetBusinessHours(DayOfWeek.Monday)!.Breaks.Count.ShouldBe(1);
        branch.GetBusinessHours(DayOfWeek.Tuesday).ShouldBeNull();
    }

    [Fact]
    public void SetBusinessHours_on_an_open_weekday_should_replace_it()
    {
        var branch = CreateBranch();
        branch.SetBusinessHours(Schedule(DayOfWeek.Monday, Lunch()));

        branch.SetBusinessHours(DailySchedule.Create(DayOfWeek.Monday, Range(8, 12)));

        branch.BusinessHours.Count.ShouldBe(1);
        branch.GetBusinessHours(DayOfWeek.Monday)!.Range.ShouldBe(Range(8, 12));
        branch.GetBusinessHours(DayOfWeek.Monday)!.Breaks.ShouldBeEmpty();
    }

    [Fact]
    public void Business_hours_should_be_ordered_by_weekday()
    {
        var branch = CreateBranch();

        branch.SetBusinessHours(Schedule(DayOfWeek.Friday));
        branch.SetBusinessHours(Schedule(DayOfWeek.Monday));
        branch.SetBusinessHours(Schedule(DayOfWeek.Sunday));

        branch.BusinessHours.Select(h => h.Day).ShouldBe(
            new[] { DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Friday });
    }

    [Fact]
    public void RemoveBusinessHours_should_close_the_weekday_and_be_idempotent()
    {
        var branch = CreateBranch();
        branch.SetBusinessHours(Schedule(DayOfWeek.Monday));

        branch.RemoveBusinessHours(DayOfWeek.Monday);
        branch.RemoveBusinessHours(DayOfWeek.Monday);

        branch.BusinessHours.ShouldBeEmpty();
    }

    [Fact]
    public void IsOpenAt_should_consider_weekday_hours_and_breaks()
    {
        var branch = CreateBranch();
        branch.SetBusinessHours(Schedule(DayOfWeek.Monday, Lunch()));

        branch.IsOpenAt(DayOfWeek.Monday, new TimeOnly(10, 0)).ShouldBeTrue();
        branch.IsOpenAt(DayOfWeek.Monday, new TimeOnly(12, 30)).ShouldBeFalse();
        branch.IsOpenAt(DayOfWeek.Monday, new TimeOnly(19, 0)).ShouldBeFalse();
        branch.IsOpenAt(DayOfWeek.Tuesday, new TimeOnly(10, 0)).ShouldBeFalse();
    }

    [Fact]
    public void UpdateDetails_should_change_the_branch_data()
    {
        var branch = CreateBranch();
        var newAddress = Address.Create("Av. Paulista", "1000", "Sala 2", "Bela Vista", "São Paulo", "SP", "01310-200");

        branch.UpdateDetails("Paulista", newAddress, Phone.Create("+551144445555"), "America/Manaus");

        branch.Name.ShouldBe("Paulista");
        branch.Address.ShouldBe(newAddress);
        branch.Phone.Value.ShouldBe("+551144445555");
        branch.TimeZoneId.ShouldBe("America/Manaus");
    }

    [Fact]
    public void Invalid_update_should_leave_the_branch_unchanged()
    {
        var branch = CreateBranch();

        Should.Throw<DomainException>(() => branch.UpdateDetails(
            "Paulista",
            CreateAddress(),
            Phone.Create("+551144445555"),
            "Mars/Olympus"));

        branch.Name.ShouldBe("Centro");
        branch.Phone.Value.ShouldBe("+551133334444");
        branch.TimeZoneId.ShouldBe("America/Sao_Paulo");
    }

    [Fact]
    public void Deactivate_and_activate_should_toggle_the_flag()
    {
        var branch = CreateBranch();

        branch.Deactivate();
        branch.IsActive.ShouldBeFalse();

        branch.Activate();
        branch.IsActive.ShouldBeTrue();
    }
}