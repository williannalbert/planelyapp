using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Professionals.Deactivate;
using PlanelyApp.Application.Organization.Professionals.Get;
using PlanelyApp.Application.Organization.Professionals.List;
using PlanelyApp.Application.Organization.Professionals.Register;
using PlanelyApp.Application.Organization.Professionals.RemoveWorkingHours;
using PlanelyApp.Application.Organization.Professionals.SetWorkingHours;
using PlanelyApp.Application.Organization.Shared;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Organization;

/// <summary>The whole way, as a request has it: dispatcher, decorators, handler, repository or read model, and MySQL.</summary>
public class ProfessionalUseCaseTests(MySqlFixture mysql)
{
    [Fact]
    public async Task Registering_a_professional_should_save_it_in_the_company_of_the_request()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        var id = await RegisterAsync(company.Id, "Ana Souza", cancellationToken);

        var professional = await LoadAsync(company.Id, id, cancellationToken);

        professional.ShouldNotBeNull();
        professional.CompanyId.ShouldBe(company.Id);
        professional.Name.ShouldBe("Ana Souza");
        professional.Specialty.ShouldBe("Cabeleireira");
        professional.UserId.ShouldBeNull();
        professional.IsActive.ShouldBeTrue();
        professional.ScheduleVersion.ShouldBe(0);
    }

    [Fact]
    public async Task The_working_hours_should_be_saved_with_their_breaks_and_raise_the_agenda_version()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var branch = await StaffData.SeedBranchAsync(mysql, company.Id, cancellationToken);
        var id = await RegisterAsync(company.Id, "Ana Souza", cancellationToken);
        var monday = new DailyScheduleData(
            DayOfWeek.Monday,
            new TimeOnly(9, 0),
            new TimeOnly(18, 0),
            [new BreakData(new TimeOnly(12, 0), new TimeOnly(13, 0), "Lunch")]);

        (await SendAsync(company.Id, new SetProfessionalWorkingHoursCommand(id, branch.Id, monday), cancellationToken))
            .IsSuccess.ShouldBeTrue();

        var dto = (await QueryAsync(company.Id, new GetProfessionalQuery(id), cancellationToken)).Value;
        var hours = dto.WorkingHours.ShouldHaveSingleItem();
        hours.BranchId.ShouldBe(branch.Id);
        hours.Schedule.Day.ShouldBe(DayOfWeek.Monday);
        hours.Schedule.Breaks.ShouldHaveSingleItem().Description.ShouldBe("Lunch");
        (await LoadAsync(company.Id, id, cancellationToken))!.ScheduleVersion.ShouldBe(1);
    }

    [Fact]
    public async Task Overlapping_hours_at_two_branches_should_be_refused_and_the_first_ones_kept()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var first = await StaffData.SeedBranchAsync(mysql, company.Id, cancellationToken);
        var second = await StaffData.SeedBranchAsync(mysql, company.Id, cancellationToken);
        var id = await RegisterAsync(company.Id, "Ana Souza", cancellationToken);

        (await SendAsync(company.Id, SetHours(id, first.Id, DayOfWeek.Monday, 9, 18), cancellationToken))
            .IsSuccess.ShouldBeTrue();

        var exception = await Should.ThrowAsync<DomainException>(
            () => SendAsync(company.Id, SetHours(id, second.Id, DayOfWeek.Monday, 17, 20), cancellationToken));

        exception.Code.ShouldBe("Professional.WorkingHoursConflict");
        var dto = (await QueryAsync(company.Id, new GetProfessionalQuery(id), cancellationToken)).Value;
        dto.WorkingHours.ShouldHaveSingleItem().BranchId.ShouldBe(first.Id);
    }

    [Fact]
    public async Task Hours_at_a_branch_of_another_company_should_not_be_accepted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var foreignBranch = await StaffData.SeedBranchAsync(mysql, other.Id, cancellationToken);
        var id = await RegisterAsync(company.Id, "Ana Souza", cancellationToken);

        var result = await SendAsync(
            company.Id, SetHours(id, foreignBranch.Id, DayOfWeek.Monday, 9, 18), cancellationToken);

        result.Error.Code.ShouldBe("Branch.NotFound");
        var dto = (await QueryAsync(company.Id, new GetProfessionalQuery(id), cancellationToken)).Value;
        dto.WorkingHours.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_professional_of_another_company_should_not_be_found_or_changed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var id = await RegisterAsync(company.Id, "Ana Souza", cancellationToken);

        var read = await QueryAsync(other.Id, new GetProfessionalQuery(id), cancellationToken);
        var change = await SendAsync(other.Id, new DeactivateProfessionalCommand(id), cancellationToken);

        read.Error.Code.ShouldBe("Professional.NotFound");
        change.Error.Code.ShouldBe("Professional.NotFound");
        (await QueryAsync(company.Id, new GetProfessionalQuery(id), cancellationToken)).Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Removing_a_weekday_should_be_saved_and_raise_the_agenda_version_again()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var branch = await StaffData.SeedBranchAsync(mysql, company.Id, cancellationToken);
        var id = await RegisterAsync(company.Id, "Ana Souza", cancellationToken);

        await SendAsync(company.Id, SetHours(id, branch.Id, DayOfWeek.Monday, 9, 18), cancellationToken);
        await SendAsync(company.Id, SetHours(id, branch.Id, DayOfWeek.Tuesday, 10, 19), cancellationToken);
        (await SendAsync(
            company.Id, new RemoveProfessionalWorkingHoursCommand(id, branch.Id, DayOfWeek.Monday), cancellationToken))
            .IsSuccess.ShouldBeTrue();

        var dto = (await QueryAsync(company.Id, new GetProfessionalQuery(id), cancellationToken)).Value;
        dto.WorkingHours.ShouldHaveSingleItem().Schedule.Day.ShouldBe(DayOfWeek.Tuesday);
        (await LoadAsync(company.Id, id, cancellationToken))!.ScheduleVersion.ShouldBe(3);
    }

    [Fact]
    public async Task Listing_should_show_only_the_professionals_of_the_company_in_order_and_honor_the_filter()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var bruno = await RegisterAsync(company.Id, "Bruno Alves", cancellationToken);
        await RegisterAsync(company.Id, "Ana Souza", cancellationToken);
        await RegisterAsync(other.Id, "Carla Dias", cancellationToken);
        (await SendAsync(company.Id, new DeactivateProfessionalCommand(bruno), cancellationToken))
            .IsSuccess.ShouldBeTrue();

        var all = (await QueryAsync(company.Id, new ListProfessionalsQuery(), cancellationToken)).Value;
        var active = (await QueryAsync(company.Id, new ListProfessionalsQuery(IsActive: true), cancellationToken)).Value;
        var inactive = (await QueryAsync(company.Id, new ListProfessionalsQuery(IsActive: false), cancellationToken)).Value;
        var ofOther = (await QueryAsync(other.Id, new ListProfessionalsQuery(), cancellationToken)).Value;

        all.Select(professional => professional.Name).ShouldBe(["Ana Souza", "Bruno Alves"], ignoreOrder: false);
        all[0].Specialty.ShouldBe("Cabeleireira");
        active.Select(professional => professional.Name).ShouldBe(["Ana Souza"], ignoreOrder: false);
        inactive.Select(professional => professional.Name).ShouldBe(["Bruno Alves"], ignoreOrder: false);
        ofOther.ShouldHaveSingleItem().Name.ShouldBe("Carla Dias");
    }

    private static SetProfessionalWorkingHoursCommand SetHours(
        Guid professionalId,
        Guid branchId,
        DayOfWeek day,
        int fromHour,
        int toHour) => new(
            professionalId,
            branchId,
            new DailyScheduleData(day, new TimeOnly(fromHour, 0), new TimeOnly(toHour, 0), []));

    private async Task<Guid> RegisterAsync(Guid companyId, string name, CancellationToken cancellationToken)
    {
        var result = await SendAsync(companyId, new RegisterProfessionalCommand(name, "Cabeleireira"), cancellationToken);

        return result.Value;
    }

    private async Task<Professional?> LoadAsync(Guid companyId, Guid id, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);

        return await scope.ServiceProvider.GetRequiredService<IProfessionalRepository>()
            .GetByIdAsync(id, cancellationToken);
    }

    // One scope per call: each one is a request, with its own context and the company set.
    private async Task<Result<TResponse>> SendAsync<TResponse>(
        Guid companyId,
        ICommand<TResponse> command,
        CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);

        return await scope.ServiceProvider.GetRequiredService<IDispatcher>().SendAsync(command, cancellationToken);
    }

    private async Task<Result<TResponse>> QueryAsync<TResponse>(
        Guid companyId,
        IQuery<TResponse> query,
        CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);

        return await scope.ServiceProvider.GetRequiredService<IDispatcher>().QueryAsync(query, cancellationToken);
    }
}