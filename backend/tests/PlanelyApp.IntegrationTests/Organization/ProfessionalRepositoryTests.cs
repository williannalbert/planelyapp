using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Organization;

public class ProfessionalRepositoryTests(MySqlFixture mysql)
{
    [Fact]
    public async Task A_saved_professional_should_come_back_with_everything_it_owns()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var branchA = await StaffData.SeedBranchAsync(mysql, company.Id, cancellationToken);
        var branchB = await StaffData.SeedBranchAsync(mysql, company.Id, cancellationToken);

        var user = StaffData.NewUser(company.Id);
        await mysql.SaveAsync(null, cancellationToken, s => s.GetRequiredService<IUserRepository>().Add(user));

        var start = new DateTime(2027, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        var serviceA = Guid.CreateVersion7();
        var serviceB = Guid.CreateVersion7();

        var professional = Professional.Create(company.Id, "Ana Souza", "Barbeira", user.Id);
        professional.SetWorkingHours(branchA.Id, OrganizationData.WorkDay(DayOfWeek.Monday));
        professional.SetWorkingHours(branchB.Id, OrganizationData.WorkDay(DayOfWeek.Tuesday, withBreaks: false));
        professional.AddTimeOff(DateTimeRange.Create(start, start.AddDays(3)), "Vacation");
        professional.AddTimeOff(DateTimeRange.Create(start.AddDays(30), start.AddDays(31)));
        professional.EnableOffering(serviceA, TimeSpan.FromMinutes(45));
        professional.EnableOffering(serviceB);
        await SaveAsync(company.Id, professional, cancellationToken);

        var loaded = await LoadAsync(company.Id, professional.Id, cancellationToken);

        loaded.ShouldNotBeNull();
        loaded.CompanyId.ShouldBe(company.Id);
        loaded.Name.ShouldBe("Ana Souza");
        loaded.Specialty.ShouldBe("Barbeira");
        loaded.UserId.ShouldBe(user.Id);
        loaded.IsActive.ShouldBeTrue();
        loaded.ScheduleVersion.ShouldBe(professional.ScheduleVersion);

        loaded.WorkingHours.Count.ShouldBe(2);
        loaded.GetWorkingHours(branchA.Id, DayOfWeek.Monday).ShouldBe(professional.GetWorkingHours(branchA.Id, DayOfWeek.Monday));
        loaded.GetWorkingHours(branchB.Id, DayOfWeek.Tuesday).ShouldBe(professional.GetWorkingHours(branchB.Id, DayOfWeek.Tuesday));
        loaded.GetWorkingHours(branchA.Id, DayOfWeek.Monday)!.Breaks.Count.ShouldBe(2);
        loaded.GetWorkingHours(branchB.Id, DayOfWeek.Monday).ShouldBeNull();

        loaded.TimeOffs.Count.ShouldBe(2);
        loaded.TimeOffs.Select(t => t.Id).Order().ShouldBe(professional.TimeOffs.Select(t => t.Id).Order());
        var vacation = loaded.TimeOffs.Single(t => t.Reason == "Vacation");
        vacation.Range.ShouldBe(DateTimeRange.Create(start, start.AddDays(3)));
        vacation.Range.Start.Kind.ShouldBe(DateTimeKind.Utc);

        loaded.Offerings.Count.ShouldBe(2);
        loaded.GetOffering(serviceA)!.CustomDuration.ShouldBe(TimeSpan.FromMinutes(45));
        loaded.GetOffering(serviceB)!.CustomDuration.ShouldBeNull();
        loaded.CanPerform(Guid.CreateVersion7()).ShouldBeFalse();
    }

    [Fact]
    public async Task Replacing_and_removing_what_a_professional_owns_should_be_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var branch = await StaffData.SeedBranchAsync(mysql, company.Id, cancellationToken);
        var start = new DateTime(2027, 2, 1, 9, 0, 0, DateTimeKind.Utc);
        var serviceA = Guid.CreateVersion7();
        var serviceB = Guid.CreateVersion7();

        var professional = Professional.Create(company.Id, "Bruno Lima");
        professional.SetWorkingHours(branch.Id, OrganizationData.WorkDay(DayOfWeek.Monday));
        var timeOff = professional.AddTimeOff(DateTimeRange.Create(start, start.AddDays(1)));
        professional.EnableOffering(serviceA, TimeSpan.FromMinutes(45));
        await SaveAsync(company.Id, professional, cancellationToken);

        var shorter = DailySchedule.Create(DayOfWeek.Monday, TimeOfDayRange.Create(new TimeOnly(8, 0), new TimeOnly(12, 0)));

        await UpdateAsync(company.Id, professional.Id, cancellationToken, loaded =>
        {
            loaded.SetWorkingHours(branch.Id, shorter);          // replaces the Monday that had breaks
            loaded.RemoveTimeOff(timeOff.Id);
            loaded.EnableOffering(serviceA, TimeSpan.FromMinutes(30)); // replaces the existing offering
            loaded.EnableOffering(serviceB);
        });

        var afterReplace = await LoadAsync(company.Id, professional.Id, cancellationToken);

        afterReplace.ShouldNotBeNull();
        afterReplace.WorkingHours.Count.ShouldBe(1);
        afterReplace.GetWorkingHours(branch.Id, DayOfWeek.Monday).ShouldBe(shorter);
        afterReplace.GetWorkingHours(branch.Id, DayOfWeek.Monday)!.Breaks.ShouldBeEmpty();
        afterReplace.TimeOffs.ShouldBeEmpty();
        afterReplace.Offerings.Count.ShouldBe(2);
        afterReplace.GetOffering(serviceA)!.CustomDuration.ShouldBe(TimeSpan.FromMinutes(30));

        await UpdateAsync(company.Id, professional.Id, cancellationToken, loaded =>
        {
            loaded.RemoveWorkingHours(branch.Id, DayOfWeek.Monday);
            loaded.DisableOffering(serviceB);
        });

        var afterRemove = await LoadAsync(company.Id, professional.Id, cancellationToken);

        afterRemove.ShouldNotBeNull();
        afterRemove.WorkingHours.ShouldBeEmpty();
        afterRemove.Offerings.ShouldHaveSingleItem().ServiceOfferingId.ShouldBe(serviceA);
    }

    [Fact]
    public async Task Two_simultaneous_changes_to_the_same_agenda_should_not_both_be_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var professional = Professional.Create(company.Id, "Carla Dias");
        await SaveAsync(company.Id, professional, cancellationToken);

        var start = new DateTime(2027, 3, 1, 9, 0, 0, DateTimeKind.Utc);

        await using var first = mysql.CreateScope(company.Id);
        await using var second = mysql.CreateScope(company.Id);

        var seenByFirst = await first.ServiceProvider.GetRequiredService<IProfessionalRepository>()
            .GetByIdAsync(professional.Id, cancellationToken);
        var seenBySecond = await second.ServiceProvider.GetRequiredService<IProfessionalRepository>()
            .GetByIdAsync(professional.Id, cancellationToken);

        seenByFirst!.AddTimeOff(DateTimeRange.Create(start, start.AddHours(2)), "First");
        await first.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);

        seenBySecond!.AddTimeOff(DateTimeRange.Create(start, start.AddHours(1)), "Second");

        await Should.ThrowAsync<DbUpdateConcurrencyException>(
            () => second.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken));

        // The second transaction was rolled back as a whole.
        var final = await LoadAsync(company.Id, professional.Id, cancellationToken);
        final!.TimeOffs.ShouldHaveSingleItem().Reason.ShouldBe("First");
    }

    [Fact]
    public async Task A_professional_should_only_be_found_by_its_own_company()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var professional = Professional.Create(company.Id, "Diego Alves");
        await SaveAsync(company.Id, professional, cancellationToken);

        (await LoadAsync(company.Id, professional.Id, cancellationToken)).ShouldNotBeNull();
        (await LoadAsync(other.Id, professional.Id, cancellationToken)).ShouldBeNull();
        (await LoadAsync(null, professional.Id, cancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task A_login_should_belong_to_a_single_professional()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var user = StaffData.NewUser(company.Id);
        await mysql.SaveAsync(null, cancellationToken, s => s.GetRequiredService<IUserRepository>().Add(user));

        await SaveAsync(company.Id, Professional.Create(company.Id, "Elisa Melo", userId: user.Id), cancellationToken);

        await Should.ThrowAsync<DbUpdateException>(
            () => SaveAsync(company.Id, Professional.Create(company.Id, "Fábio Reis", userId: user.Id), cancellationToken));
    }

    [Fact]
    public async Task Professionals_without_a_login_should_not_conflict_with_each_other()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        await SaveAsync(company.Id, Professional.Create(company.Id, "Gabi Nunes"), cancellationToken);
        await SaveAsync(company.Id, Professional.Create(company.Id, "Hugo Prado"), cancellationToken);
    }

    [Fact]
    public async Task Working_hours_at_a_branch_that_does_not_exist_should_not_be_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        var professional = Professional.Create(company.Id, "Iara Costa");
        professional.SetWorkingHours(Guid.CreateVersion7(), OrganizationData.WorkDay(DayOfWeek.Monday));

        await Should.ThrowAsync<DbUpdateException>(() => SaveAsync(company.Id, professional, cancellationToken));
    }

    [Fact]
    public async Task The_longest_values_the_domain_allows_should_fit_in_the_columns()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var start = new DateTime(2027, 4, 1, 9, 0, 0, DateTimeKind.Utc);

        var professional = Professional.Create(company.Id, new string('N', 100), new string('E', 100));
        professional.AddTimeOff(DateTimeRange.Create(start, start.AddDays(1)), new string('R', 200));
        await SaveAsync(company.Id, professional, cancellationToken);

        var loaded = await LoadAsync(company.Id, professional.Id, cancellationToken);

        loaded.ShouldNotBeNull();
        loaded.Name.Length.ShouldBe(100);
        loaded.Specialty!.Length.ShouldBe(100);
        loaded.TimeOffs.ShouldHaveSingleItem().Reason!.Length.ShouldBe(200);
    }

    private Task SaveAsync(Guid companyId, Professional professional, CancellationToken cancellationToken) =>
        mysql.SaveAsync(companyId, cancellationToken,
            services => services.GetRequiredService<IProfessionalRepository>().Add(professional));

    private async Task UpdateAsync(Guid companyId, Guid id, CancellationToken cancellationToken, Action<Professional> change)
    {
        await using var scope = mysql.CreateScope(companyId);
        var loaded = await scope.ServiceProvider.GetRequiredService<IProfessionalRepository>().GetByIdAsync(id, cancellationToken);

        change(loaded!);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
    }

    private async Task<Professional?> LoadAsync(Guid? companyId, Guid id, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);

        return await scope.ServiceProvider.GetRequiredService<IProfessionalRepository>().GetByIdAsync(id, cancellationToken);
    }
}