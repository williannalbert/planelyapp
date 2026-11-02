using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.BranchOfferings.ClearPrice;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;

namespace PlanelyApp.Application.Tests.Catalog;

public class ClearBranchOfferingPriceTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public async Task Clearing_should_take_the_price_of_the_branch_away()
    {
        var branch = BranchData.NewBranch(_companyId);
        var service = CatalogData.NewFixedService(_companyId);
        branch.SetCustomPrice(service, Money.From(40m));

        var result = await Handler(branch, service).HandleAsync(
            new ClearBranchOfferingPriceCommand(branch.Id, service.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        branch.GetCustomPrice(service.Id).ShouldBeNull();
        branch.OfferingOverrides.ShouldBeEmpty();
    }

    [Fact]
    public async Task Clearing_a_price_that_was_never_set_should_succeed_and_store_nothing()
    {
        var branch = BranchData.NewBranch(_companyId);
        var service = CatalogData.NewFixedService(_companyId);

        var result = await Handler(branch, service).HandleAsync(
            new ClearBranchOfferingPriceCommand(branch.Id, service.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        branch.OfferingOverrides.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_missing_branch_should_be_not_found()
    {
        var result = await Handler(null, CatalogData.NewFixedService(_companyId)).HandleAsync(
            new ClearBranchOfferingPriceCommand(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Branch.NotFound");
    }

    [Fact]
    public async Task A_missing_service_should_be_not_found()
    {
        var result = await Handler(BranchData.NewBranch(_companyId), null).HandleAsync(
            new ClearBranchOfferingPriceCommand(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("ServiceOffering.NotFound");
    }

    [Fact]
    public async Task Missing_ids_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new ClearBranchOfferingPriceValidator(), new ClearBranchOfferingPriceCommand(Guid.Empty, Guid.Empty));

        problems.ShouldBe(
            ["BranchId:Validation.Required", "ServiceOfferingId:Validation.Required"], ignoreOrder: true);
    }

    private static ClearBranchOfferingPriceHandler Handler(Branch? branch, ServiceOffering? service) =>
        new(BranchData.RepositoryWith(branch), CatalogData.ServicesWith(service));
}