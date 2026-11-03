using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.Pricing.GetPrice;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;

namespace PlanelyApp.Application.Tests.Catalog;

public class GetPriceTests
{
    // 2027-11-22 is a Monday: 13:00 UTC is 10:00 at the branch (São Paulo, UTC-3).
    private static readonly DateTime Monday = new(2027, 11, 22, 13, 0, 0, DateTimeKind.Utc);

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly IPromotionRepository _promotions = Substitute.For<IPromotionRepository>();
    private readonly Branch _branch;

    public GetPriceTests()
    {
        _branch = BranchData.NewBranch(_companyId);
        InForce();
    }

    [Fact]
    public async Task Without_promotions_each_line_should_cost_its_price_and_the_totals_add_up()
    {
        var haircut = CatalogData.NewFixedService(_companyId);
        var beard = CatalogData.NewFixedService(_companyId, "Beard", 30m);

        var result = await Handler(haircut, beard).HandleAsync(
            Query(haircut.Id, beard.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var quote = result.Value;
        quote.Lines.Select(line => line.ServiceName).ShouldBe(["Haircut", "Beard"], ignoreOrder: false);
        var line = quote.Lines[0];
        line.ServiceOfferingId.ShouldBe(haircut.Id);
        line.IsOnRequest.ShouldBeFalse();
        line.ListPrice.ShouldBe(50m);
        line.Discount.ShouldBe(0m);
        line.FinalPrice.ShouldBe(50m);
        line.PromotionId.ShouldBeNull();
        line.PromotionName.ShouldBeNull();
        line.IsFromBundle.ShouldBeFalse();
        line.IsAvailable.ShouldBeTrue();
        quote.ListTotal.ShouldBe(80m);
        quote.DiscountTotal.ShouldBe(0m);
        quote.FinalTotal.ShouldBe(80m);
        quote.HasServiceOnRequest.ShouldBeFalse();
    }

    [Fact]
    public async Task The_price_of_the_branch_should_be_used_when_it_has_one()
    {
        var haircut = CatalogData.NewFixedService(_companyId);
        _branch.SetCustomPrice(haircut, Money.From(40m));

        var result = await Handler(haircut).HandleAsync(Query(haircut.Id), TestContext.Current.CancellationToken);

        var line = result.Value.Lines.Single();
        line.ListPrice.ShouldBe(40m);
        line.FinalPrice.ShouldBe(40m);
    }

    [Fact]
    public async Task A_promotion_should_discount_its_service_and_name_the_promotion()
    {
        var haircut = CatalogData.NewFixedService(_companyId);
        var promotion = PromotionData.NewPromotionOf(_companyId, haircut);
        InForce(promotion);

        var result = await Handler(haircut).HandleAsync(Query(haircut.Id), TestContext.Current.CancellationToken);

        var line = result.Value.Lines.Single();
        line.ListPrice.ShouldBe(50m);
        line.Discount.ShouldBe(5m);
        line.FinalPrice.ShouldBe(45m);
        line.PromotionId.ShouldBe(promotion.Id);
        line.PromotionName.ShouldBe("Black Friday");
        line.IsFromBundle.ShouldBeFalse();
        result.Value.DiscountTotal.ShouldBe(5m);
        result.Value.FinalTotal.ShouldBe(45m);
    }

    [Fact]
    public async Task A_bundle_should_share_its_discount_among_the_services()
    {
        var haircut = CatalogData.NewFixedService(_companyId);
        var beard = CatalogData.NewFixedService(_companyId, "Beard", 30m);
        var bundle = PromotionData.NewBundleOf(_companyId, haircut, beard);
        InForce(bundle);

        var result = await Handler(haircut, beard).HandleAsync(
            Query(haircut.Id, beard.Id), TestContext.Current.CancellationToken);

        var quote = result.Value;
        quote.Lines.ShouldAllBe(line => line.IsFromBundle && line.PromotionId == bundle.Id && line.PromotionName == "Combo");
        quote.Lines[0].Discount.ShouldBe(5m);
        quote.Lines[1].Discount.ShouldBe(3m);
        quote.ListTotal.ShouldBe(80m);
        quote.DiscountTotal.ShouldBe(8m);
        quote.FinalTotal.ShouldBe(72m);
    }

    [Fact]
    public async Task A_service_on_request_should_have_no_price_and_stay_out_of_the_totals()
    {
        var haircut = CatalogData.NewFixedService(_companyId);
        var tattoo = CatalogData.NewOnRequestService(_companyId);

        var result = await Handler(haircut, tattoo).HandleAsync(
            Query(haircut.Id, tattoo.Id), TestContext.Current.CancellationToken);

        var quote = result.Value;
        var line = quote.Lines[1];
        line.IsOnRequest.ShouldBeTrue();
        line.ListPrice.ShouldBeNull();
        line.FinalPrice.ShouldBeNull();
        line.Discount.ShouldBe(0m);
        line.IsAvailable.ShouldBeTrue();
        quote.ListTotal.ShouldBe(50m);
        quote.FinalTotal.ShouldBe(50m);
        quote.HasServiceOnRequest.ShouldBeTrue();
    }

    [Fact]
    public async Task A_repeated_service_should_have_a_line_each_in_the_order_asked_and_be_loaded_once()
    {
        var haircut = CatalogData.NewFixedService(_companyId);
        var beard = CatalogData.NewFixedService(_companyId, "Beard", 30m);
        var stored = PromotionData.ServicesListing(haircut, beard);
        var handler = new GetPriceHandler(BranchData.RepositoryWith(_branch), stored, _promotions);

        var result = await handler.HandleAsync(
            Query(beard.Id, haircut.Id, beard.Id), TestContext.Current.CancellationToken);

        result.Value.Lines.Select(line => line.ServiceName)
            .ShouldBe(["Beard", "Haircut", "Beard"], ignoreOrder: false);
        result.Value.ListTotal.ShouldBe(110m);
        await stored.Received(1).ListByIdsAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_promotions_should_be_looked_up_by_the_day_at_the_branch_not_the_utc_day()
    {
        var haircut = CatalogData.NewFixedService(_companyId);
        var lateNightUtc = new DateTime(2027, 11, 20, 2, 0, 0, DateTimeKind.Utc); // still November 19 at the branch

        await Handler(haircut).HandleAsync(
            new GetPriceQuery(_branch.Id, lateNightUtc, [haircut.Id]), TestContext.Current.CancellationToken);

        await _promotions.Received(1).ListInForceAsync(new DateOnly(2027, 11, 19), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_inactive_service_should_be_priced_but_not_available()
    {
        var haircut = CatalogData.NewFixedService(_companyId);
        haircut.Deactivate();

        var result = await Handler(haircut).HandleAsync(Query(haircut.Id), TestContext.Current.CancellationToken);

        var line = result.Value.Lines.Single();
        line.ListPrice.ShouldBe(50m);
        line.IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task A_service_the_branch_does_not_offer_should_be_priced_but_not_available()
    {
        var haircut = CatalogData.NewFixedService(_companyId);
        _branch.MakeOfferingUnavailable(haircut.Id);

        var result = await Handler(haircut).HandleAsync(Query(haircut.Id), TestContext.Current.CancellationToken);

        var line = result.Value.Lines.Single();
        line.ListPrice.ShouldBe(50m);
        line.IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task A_branch_that_does_not_exist_should_be_not_found_and_load_nothing()
    {
        var stored = PromotionData.ServicesListing();
        var handler = new GetPriceHandler(BranchData.RepositoryWith(null), stored, _promotions);

        var result = await handler.HandleAsync(Query(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Branch.NotFound");
        await stored.DidNotReceive().ListByIdsAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_service_that_does_not_exist_should_be_not_found_and_look_for_no_promotion()
    {
        var haircut = CatalogData.NewFixedService(_companyId);

        var result = await Handler(haircut).HandleAsync(
            Query(haircut.Id, Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("ServiceOffering.NotFound");
        await _promotions.DidNotReceive().ListInForceAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_time_that_is_not_utc_should_be_refused_by_the_domain()
    {
        var haircut = CatalogData.NewFixedService(_companyId);
        var unspecified = new DateTime(2027, 11, 22, 13, 0, 0);

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(haircut).HandleAsync(
            new GetPriceQuery(_branch.Id, unspecified, [haircut.Id]), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Branch.TimeMustBeUtc");
    }

    [Fact]
    public async Task A_valid_query_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new GetPriceValidator(), new GetPriceQuery(Guid.NewGuid(), Monday, [Guid.NewGuid()]));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task What_is_missing_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new GetPriceValidator(), new GetPriceQuery(Guid.Empty, default, []));

        problems.ShouldBe(
            ["BranchId:Validation.Required", "StartUtc:Validation.Required", "ServiceIds:Validation.Required"],
            ignoreOrder: true);
    }

    [Fact]
    public async Task An_empty_service_id_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new GetPriceValidator(), new GetPriceQuery(Guid.NewGuid(), Monday, [Guid.NewGuid(), Guid.Empty]));

        problems.ShouldBe(["ServiceIds[1]:Validation.Invalid"], ignoreOrder: true);
    }

    [Fact]
    public async Task More_than_fifty_services_should_be_reported_as_invalid()
    {
        var ids = Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToList();

        var problems = await TestValidation.RunAsync(
            new GetPriceValidator(), new GetPriceQuery(Guid.NewGuid(), Monday, ids));

        problems.ShouldBe(["ServiceIds:Validation.Invalid"], ignoreOrder: true);
    }

    private GetPriceQuery Query(params Guid[] serviceIds) => new(_branch.Id, Monday, serviceIds);

    private GetPriceHandler Handler(params ServiceOffering[] stored) =>
        new(BranchData.RepositoryWith(_branch), PromotionData.ServicesListing(stored), _promotions);

    private void InForce(params Promotion[] promotions) =>
        _promotions.ListInForceAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(promotions);
}