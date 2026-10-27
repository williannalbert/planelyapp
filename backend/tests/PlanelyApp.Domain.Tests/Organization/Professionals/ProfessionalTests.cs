using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Organization.Professionals;

public class ProfessionalTests
{
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly Guid BranchA = Guid.CreateVersion7();
    private static readonly Guid BranchB = Guid.CreateVersion7();

    private static Professional CreateProfessional(
        Guid? companyId = null,
        string? name = "Rafael",
        string? specialty = "Tatuador",
        Guid? userId = null) =>
        Professional.Create(companyId ?? CompanyId, name, specialty, userId);

    private static DailySchedule Schedule(DayOfWeek day, int startHour, int endHour) =>
        DailySchedule.Create(
            day,
            TimeOfDayRange.Create(new TimeOnly(startHour, 0), new TimeOnly(endHour, 0)));

    [Fact]
    public void Create_should_register_an_active_professional_without_working_hours()
    {
        var professional = CreateProfessional(name: "  Rafael ", specialty: " Tatuador ");

        professional.Id.Version.ShouldBe(7);
        professional.CompanyId.ShouldBe(CompanyId);
        professional.Name.ShouldBe("Rafael");
        professional.Specialty.ShouldBe("Tatuador");
        professional.UserId.ShouldBeNull();
        professional.IsActive.ShouldBeTrue();
        professional.WorkingHours.ShouldBeEmpty();
    }

    [Fact]
    public void Professional_should_be_a_tenant_entity()
    {
        ITenantEntity tenantEntity = CreateProfessional();

        tenantEntity.CompanyId.ShouldBe(CompanyId);
    }

    [Fact]
    public void Create_with_a_user_should_link_it()
    {
        var userId = Guid.CreateVersion7();

        CreateProfessional(userId: userId).UserId.ShouldBe(userId);
    }

    [Fact]
    public void Empty_company_id_should_be_rejected()
    {
        Should.Throw<DomainException>(() => CreateProfessional(companyId: Guid.Empty))
            .Code.ShouldBe("Professional.CompanyRequired");
    }

    [Fact]
    public void Empty_user_id_should_be_rejected()
    {
        Should.Throw<DomainException>(() => CreateProfessional(userId: Guid.Empty))
            .Code.ShouldBe("Professional.UserInvalid");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_name_should_be_rejected(string? name)
    {
        Should.Throw<DomainException>(() => CreateProfessional(name: name))
            .Code.ShouldBe("Professional.NameInvalid");
    }

    [Fact]
    public void Name_and_specialty_above_the_limit_should_be_rejected()
    {
        Should.Throw<DomainException>(() => CreateProfessional(name: new string('a', 101)))
            .Code.ShouldBe("Professional.NameInvalid");
        Should.Throw<DomainException>(() => CreateProfessional(specialty: new string('a', 101)))
            .Code.ShouldBe("Professional.SpecialtyInvalid");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_specialty_should_become_null(string? specialty)
    {
        CreateProfessional(specialty: specialty).Specialty.ShouldBeNull();
    }

    [Fact]
    public void UpdateDetails_should_change_name_and_specialty()
    {
        var professional = CreateProfessional();

        professional.UpdateDetails("Rafael Lima", null);

        professional.Name.ShouldBe("Rafael Lima");
        professional.Specialty.ShouldBeNull();
    }

    [Fact]
    public void Invalid_update_should_leave_the_professional_unchanged()
    {
        var professional = CreateProfessional();

        Should.Throw<DomainException>(() => professional.UpdateDetails("Rafael Lima", new string('a', 101)));

        professional.Name.ShouldBe("Rafael");
        professional.Specialty.ShouldBe("Tatuador");
    }

    [Fact]
    public void LinkUser_and_UnlinkUser_should_change_the_login()
    {
        var professional = CreateProfessional();
        var userId = Guid.CreateVersion7();

        professional.LinkUser(userId);
        professional.UserId.ShouldBe(userId);

        professional.UnlinkUser();
        professional.UserId.ShouldBeNull();
    }

    [Fact]
    public void LinkUser_with_empty_id_should_be_rejected()
    {
        Should.Throw<DomainException>(() => CreateProfessional().LinkUser(Guid.Empty))
            .Code.ShouldBe("Professional.UserInvalid");
    }

    [Fact]
    public void SetWorkingHours_should_add_the_working_day()
    {
        var professional = CreateProfessional();

        professional.SetWorkingHours(BranchA, Schedule(DayOfWeek.Monday, 9, 18));

        professional.WorkingHours.Count.ShouldBe(1);
        professional.GetWorkingHours(BranchA, DayOfWeek.Monday)!.Range.Start.ShouldBe(new TimeOnly(9, 0));
        professional.GetWorkingHours(BranchA, DayOfWeek.Tuesday).ShouldBeNull();
        professional.GetWorkingHours(BranchB, DayOfWeek.Monday).ShouldBeNull();
    }

    [Fact]
    public void SetWorkingHours_for_the_same_branch_and_weekday_should_replace_it()
    {
        var professional = CreateProfessional();
        professional.SetWorkingHours(BranchA, Schedule(DayOfWeek.Monday, 9, 18));

        professional.SetWorkingHours(BranchA, Schedule(DayOfWeek.Monday, 10, 14));

        professional.WorkingHours.Count.ShouldBe(1);
        professional.GetWorkingHours(BranchA, DayOfWeek.Monday)!.Range.Start.ShouldBe(new TimeOnly(10, 0));
    }

    [Fact]
    public void Same_weekday_at_different_branches_without_overlap_should_be_accepted()
    {
        var professional = CreateProfessional();

        professional.SetWorkingHours(BranchA, Schedule(DayOfWeek.Monday, 9, 13));
        professional.SetWorkingHours(BranchB, Schedule(DayOfWeek.Monday, 13, 18));

        professional.WorkingHours.Count.ShouldBe(2);
    }

    [Fact]
    public void Same_weekday_at_different_branches_with_overlap_should_be_rejected()
    {
        var professional = CreateProfessional();
        professional.SetWorkingHours(BranchA, Schedule(DayOfWeek.Monday, 9, 13));

        Should.Throw<DomainException>(() =>
                professional.SetWorkingHours(BranchB, Schedule(DayOfWeek.Monday, 12, 18)))
            .Code.ShouldBe("Professional.WorkingHoursConflict");

        professional.WorkingHours.Count.ShouldBe(1);
        professional.WorksAt(BranchB).ShouldBeFalse();
    }

    [Fact]
    public void Same_time_on_different_weekdays_at_different_branches_should_be_accepted()
    {
        var professional = CreateProfessional();

        professional.SetWorkingHours(BranchA, Schedule(DayOfWeek.Monday, 9, 18));
        professional.SetWorkingHours(BranchB, Schedule(DayOfWeek.Tuesday, 9, 18));

        professional.WorkingHours.Count.ShouldBe(2);
    }

    [Fact]
    public void Empty_branch_id_should_be_rejected()
    {
        Should.Throw<DomainException>(() =>
                CreateProfessional().SetWorkingHours(Guid.Empty, Schedule(DayOfWeek.Monday, 9, 18)))
            .Code.ShouldBe("WorkingHours.BranchRequired");
    }

    [Fact]
    public void Working_hours_should_be_ordered_by_weekday_then_start()
    {
        var professional = CreateProfessional();

        professional.SetWorkingHours(BranchA, Schedule(DayOfWeek.Friday, 9, 18));
        professional.SetWorkingHours(BranchB, Schedule(DayOfWeek.Monday, 13, 18));
        professional.SetWorkingHours(BranchA, Schedule(DayOfWeek.Monday, 8, 12));

        professional.WorkingHours
            .Select(h => $"{h.Schedule.Day}-{h.Schedule.Range.Start:HH\\:mm}")
            .ToArray()
            .ShouldBe(new[] { "Monday-08:00", "Monday-13:00", "Friday-09:00" });
    }

    [Fact]
    public void RemoveWorkingHours_should_remove_only_that_branch_and_weekday_and_be_idempotent()
    {
        var professional = CreateProfessional();
        professional.SetWorkingHours(BranchA, Schedule(DayOfWeek.Monday, 9, 12));
        professional.SetWorkingHours(BranchB, Schedule(DayOfWeek.Monday, 13, 18));
        professional.SetWorkingHours(BranchA, Schedule(DayOfWeek.Tuesday, 9, 18));

        professional.RemoveWorkingHours(BranchA, DayOfWeek.Monday);
        professional.RemoveWorkingHours(BranchA, DayOfWeek.Monday);

        professional.WorkingHours.Count.ShouldBe(2);
        professional.GetWorkingHours(BranchA, DayOfWeek.Monday).ShouldBeNull();
        professional.GetWorkingHours(BranchB, DayOfWeek.Monday).ShouldNotBeNull();
        professional.GetWorkingHours(BranchA, DayOfWeek.Tuesday).ShouldNotBeNull();
    }

    [Fact]
    public void WorksAt_should_reflect_the_branches_with_working_hours()
    {
        var professional = CreateProfessional();
        professional.SetWorkingHours(BranchA, Schedule(DayOfWeek.Monday, 9, 18));

        professional.WorksAt(BranchA).ShouldBeTrue();
        professional.WorksAt(BranchB).ShouldBeFalse();
    }

    [Fact]
    public void Deactivate_and_activate_should_toggle_the_flag()
    {
        var professional = CreateProfessional();

        professional.Deactivate();
        professional.IsActive.ShouldBeFalse();

        professional.Activate();
        professional.IsActive.ShouldBeTrue();
    }
}