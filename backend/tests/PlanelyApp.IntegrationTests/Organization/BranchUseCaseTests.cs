using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Branches.Create;
using PlanelyApp.Application.Organization.Branches.Deactivate;
using PlanelyApp.Application.Organization.Branches.Get;
using PlanelyApp.Application.Organization.Branches.List;
using PlanelyApp.Application.Organization.Branches.RemoveBusinessHours;
using PlanelyApp.Application.Organization.Branches.SetBusinessHours;
using PlanelyApp.Application.Organization.Branches.UpdateDetails;
using PlanelyApp.Application.Organization.Shared;
using PlanelyApp.Domain.Organization.Branches;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Organization;

/// <summary>The whole way, as a request has it: dispatcher, decorators, handler, repository or read model, and MySQL.</summary>
public class BranchUseCaseTests(MySqlFixture mysql)
{
    [Fact]
    public async Task Creating_a_branch_should_save_it_in_the_company_of_the_request()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        var id = await CreateAsync(company.Id, "Centro", cancellationToken);

        await using var scope = mysql.CreateScope(company.Id);
        var branch = await scope.ServiceProvider.GetRequiredService<IBranchRepository>()
            .GetByIdAsync(id, cancellationToken);

        branch.ShouldNotBeNull();
        branch.CompanyId.ShouldBe(company.Id);
        branch.Name.ShouldBe("Centro");
        branch.Address.PostalCode.ShouldBe("01310100");
        branch.TimeZoneId.ShouldBe("America/Sao_Paulo");
        branch.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task A_branch_name_should_be_unique_inside_the_company_only()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        await CreateAsync(company.Id, "Centro", cancellationToken);

        var repeated = await SendAsync(company.Id, NewBranch("CENTRO"), cancellationToken);

        repeated.Error.Type.ShouldBe(ErrorType.Conflict);
        repeated.Error.Code.ShouldBe("Branch.NameAlreadyExists");
        (await SendAsync(other.Id, NewBranch("Centro"), cancellationToken)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Renaming_to_the_name_of_another_branch_should_be_a_conflict()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        await CreateAsync(company.Id, "Centro", cancellationToken);
        var south = await CreateAsync(company.Id, "Sul", cancellationToken);

        var clash = await SendAsync(company.Id, Details(south, "Centro"), cancellationToken);
        var sameName = await SendAsync(company.Id, Details(south, "Sul"), cancellationToken);

        clash.Error.Code.ShouldBe("Branch.NameAlreadyExists");
        sameName.IsSuccess.ShouldBeTrue();

        var branch = (await QueryAsync(company.Id, new GetBranchQuery(south), cancellationToken)).Value;
        branch.Name.ShouldBe("Sul");
        branch.Phone.ShouldBe("+551155556666");
    }

    [Fact]
    public async Task The_business_hours_should_come_back_in_weekday_order_with_their_breaks()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var id = await CreateAsync(company.Id, "Centro", cancellationToken);

        var tuesday = new DailyScheduleData(DayOfWeek.Tuesday, new TimeOnly(10, 0), new TimeOnly(19, 0), []);
        var monday = new DailyScheduleData(
            DayOfWeek.Monday,
            new TimeOnly(9, 0),
            new TimeOnly(18, 0),
            [new BreakData(new TimeOnly(12, 0), new TimeOnly(13, 0), "Lunch")]);

        (await SendAsync(company.Id, new SetBranchBusinessHoursCommand(id, tuesday), cancellationToken))
            .IsSuccess.ShouldBeTrue();
        (await SendAsync(company.Id, new SetBranchBusinessHoursCommand(id, monday), cancellationToken))
            .IsSuccess.ShouldBeTrue();

        var branch = (await QueryAsync(company.Id, new GetBranchQuery(id), cancellationToken)).Value;

        branch.BusinessHours.Select(schedule => schedule.Day)
            .ShouldBe([DayOfWeek.Monday, DayOfWeek.Tuesday], ignoreOrder: false);
        var lunch = branch.BusinessHours[0].Breaks.ShouldHaveSingleItem();
        lunch.Description.ShouldBe("Lunch");
        lunch.Start.ShouldBe(new TimeOnly(12, 0));

        (await SendAsync(company.Id, new RemoveBranchBusinessHoursCommand(id, DayOfWeek.Monday), cancellationToken))
            .IsSuccess.ShouldBeTrue();

        var afterClosing = (await QueryAsync(company.Id, new GetBranchQuery(id), cancellationToken)).Value;
        afterClosing.BusinessHours.ShouldHaveSingleItem().Day.ShouldBe(DayOfWeek.Tuesday);
    }

    [Fact]
    public async Task A_branch_of_another_company_should_not_be_found_or_changed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var id = await CreateAsync(company.Id, "Centro", cancellationToken);

        var read = await QueryAsync(other.Id, new GetBranchQuery(id), cancellationToken);
        var change = await SendAsync(other.Id, new DeactivateBranchCommand(id), cancellationToken);

        read.Error.Code.ShouldBe("Branch.NotFound");
        change.Error.Code.ShouldBe("Branch.NotFound");
        (await QueryAsync(company.Id, new GetBranchQuery(id), cancellationToken)).Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Listing_should_show_only_the_branches_of_the_company_in_order_and_honor_the_filter()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var south = await CreateAsync(company.Id, "Zona Sul", cancellationToken);
        await CreateAsync(company.Id, "Centro", cancellationToken);
        await CreateAsync(other.Id, "Outra", cancellationToken);
        (await SendAsync(company.Id, new DeactivateBranchCommand(south), cancellationToken)).IsSuccess.ShouldBeTrue();

        var all = (await QueryAsync(company.Id, new ListBranchesQuery(), cancellationToken)).Value;
        var active = (await QueryAsync(company.Id, new ListBranchesQuery(IsActive: true), cancellationToken)).Value;
        var inactive = (await QueryAsync(company.Id, new ListBranchesQuery(IsActive: false), cancellationToken)).Value;
        var ofOther = (await QueryAsync(other.Id, new ListBranchesQuery(), cancellationToken)).Value;

        all.Select(branch => branch.Name).ShouldBe(["Centro", "Zona Sul"], ignoreOrder: false);
        all[0].City.ShouldBe("São Paulo");
        all[0].State.ShouldBe("SP");
        all[0].Phone.ShouldBe("+551133334444");
        all[0].IsActive.ShouldBeTrue();
        active.Select(branch => branch.Name).ShouldBe(["Centro"], ignoreOrder: false);
        inactive.Select(branch => branch.Name).ShouldBe(["Zona Sul"], ignoreOrder: false);
        ofOther.ShouldHaveSingleItem().Name.ShouldBe("Outra");
    }

    private static AddressData NewAddress() =>
        new("Rua das Flores", "123", null, "Centro", "São Paulo", "SP", "01310-100");

    private static CreateBranchCommand NewBranch(string name) =>
        new(name, NewAddress(), "+551133334444", "America/Sao_Paulo");

    private static UpdateBranchDetailsCommand Details(Guid branchId, string name) =>
        new(branchId, name, NewAddress(), "+551155556666", "America/Sao_Paulo");

    private async Task<Guid> CreateAsync(Guid companyId, string name, CancellationToken cancellationToken)
    {
        var result = await SendAsync(companyId, NewBranch(name), cancellationToken);

        return result.Value;
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