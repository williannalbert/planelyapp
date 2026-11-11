using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.BranchOfferings.SetPrice;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;

namespace PlanelyApp.Application.Tests.Catalog;

public class SetBranchOfferingPriceTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public async Task A_fixed_price_service_should_get_the_price_of_the_branch()
    {
        var branch = BranchData.NewBranch(_companyId);
        var service = CatalogData.NewFixedService(_companyId);

        var result = await Handler(branch, service).HandleAsync(
            new SetBranchOfferingPriceCommand(branch.Id, service.Id, 40m), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        branch.GetCustomPrice(service.Id)!.Amount.ShouldBe(40m);
        service.BasePrice!.Amount.ShouldBe(50m);
    }

    [Fact]
    public async Task A_new_price_should_replace_the_previous_one()
    {
        var branch = BranchData.NewBranch(_companyId);
        var service = CatalogData.NewFixedService(_companyId);
        branch.SetCustomPrice(service, Money.From(40m));

        await Handler(branch, service).HandleAsync(
            new SetBranchOfferingPriceCommand(branch.Id, service.Id, 45.5m), TestContext.Current.CancellationToken);

        branch.GetCustomPrice(service.Id)!.Amount.ShouldBe(45.5m);
        branch.OfferingOverrides.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_service_on_request_should_be_refused_by_the_domain()
    {
        var branch = BranchData.NewBranch(_companyId);
        var service = CatalogData.NewOnRequestService(_companyId);

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(branch, service).HandleAsync(
            new SetBranchOfferingPriceCommand(branch.Id, service.Id, 40m), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Branch.CustomPriceNotAllowed");
        branch.OfferingOverrides.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_service_of_another_company_should_be_refused_by_the_domain()
    {
        var branch = BranchData.NewBranch(_companyId);
        var service = CatalogData.NewFixedService(Guid.NewGuid());

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(branch, service).HandleAsync(
            new SetBranchOfferingPriceCommand(branch.Id, service.Id, 40m), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Branch.ServiceFromAnotherCompany");
    }

    [Fact]
    public async Task A_negative_price_should_be_refused_by_the_domain()
    {
        var branch = BranchData.NewBranch(_companyId);
        var service = CatalogData.NewFixedService(_companyId);

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(branch, service).HandleAsync(
            new SetBranchOfferingPriceCommand(branch.Id, service.Id, -1m), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Money.NegativeAmount");
        branch.OfferingOverrides.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_missing_branch_should_be_not_found()
    {
        var service = CatalogData.NewFixedService(_companyId);

        var result = await Handler(null, service).HandleAsync(
            new SetBranchOfferingPriceCommand(Guid.NewGuid(), service.Id, 40m), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Branch.NotFound");
    }

    [Fact]
    public async Task A_missing_service_should_be_not_found_and_change_nothing()
    {
        var branch = BranchData.NewBranch(_companyId);

        var result = await Handler(branch, null).HandleAsync(
            new SetBranchOfferingPriceCommand(branch.Id, Guid.NewGuid(), 40m), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("ServiceOffering.NotFound");
        branch.OfferingOverrides.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_valid_command_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new SetBranchOfferingPriceValidator(),
            new SetBranchOfferingPriceCommand(Guid.NewGuid(), Guid.NewGuid(), 40.5m));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task Missing_ids_and_a_price_with_too_many_decimals_should_be_reported()
    {
        var problems = await TestValidation.RunAsync(
            new SetBranchOfferingPriceValidator(),
            new SetBranchOfferingPriceCommand(Guid.Empty, Guid.Empty, 10.999m));

        problems.ShouldBe(
            ["BranchId:Validation.Required", "ServiceOfferingId:Validation.Required", "Price:Validation.Invalid"],
            ignoreOrder: true);
    }

    private static SetBranchOfferingPriceHandler Handler(Branch? branch, ServiceOffering? service) =>
        new(BranchData.RepositoryWith(branch), CatalogData.ServicesWith(service));
}