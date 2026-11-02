using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.BranchOfferings.SetAvailability;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;

namespace PlanelyApp.Application.Tests.Catalog;

public class SetBranchOfferingAvailabilityTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public async Task A_branch_should_stop_offering_a_service()
    {
        var branch = BranchData.NewBranch(_companyId);
        var service = CatalogData.NewFixedService(_companyId);

        var result = await Handler(branch, service).HandleAsync(
            new SetBranchOfferingAvailabilityCommand(branch.Id, service.Id, false),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        branch.Offers(service.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task A_branch_should_offer_the_service_again()
    {
        var branch = BranchData.NewBranch(_companyId);
        var service = CatalogData.NewFixedService(_companyId);
        branch.MakeOfferingUnavailable(service.Id);

        var result = await Handler(branch, service).HandleAsync(
            new SetBranchOfferingAvailabilityCommand(branch.Id, service.Id, true),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        branch.Offers(service.Id).ShouldBeTrue();
        branch.OfferingOverrides.ShouldBeEmpty();
    }

    [Fact]
    public async Task Making_a_service_unavailable_should_keep_the_price_of_the_branch()
    {
        var branch = BranchData.NewBranch(_companyId);
        var service = CatalogData.NewFixedService(_companyId);
        branch.SetCustomPrice(service, Money.From(40m));

        await Handler(branch, service).HandleAsync(
            new SetBranchOfferingAvailabilityCommand(branch.Id, service.Id, false),
            TestContext.Current.CancellationToken);

        branch.GetCustomPrice(service.Id)!.Amount.ShouldBe(40m);
    }

    [Fact]
    public async Task A_missing_branch_should_be_not_found()
    {
        var result = await Handler(null, CatalogData.NewFixedService(_companyId)).HandleAsync(
            new SetBranchOfferingAvailabilityCommand(Guid.NewGuid(), Guid.NewGuid(), false),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Branch.NotFound");
    }

    [Fact]
    public async Task A_missing_service_should_be_not_found()
    {
        var result = await Handler(BranchData.NewBranch(_companyId), null).HandleAsync(
            new SetBranchOfferingAvailabilityCommand(Guid.NewGuid(), Guid.NewGuid(), false),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("ServiceOffering.NotFound");
    }

    [Fact]
    public async Task Missing_ids_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new SetBranchOfferingAvailabilityValidator(),
            new SetBranchOfferingAvailabilityCommand(Guid.Empty, Guid.Empty, true));

        problems.ShouldBe(
            ["BranchId:Validation.Required", "ServiceOfferingId:Validation.Required"], ignoreOrder: true);
    }

    private static SetBranchOfferingAvailabilityHandler Handler(Branch? branch, ServiceOffering? service) =>
        new(BranchData.RepositoryWith(branch), CatalogData.ServicesWith(service));
}