using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.Promotions;
using PlanelyApp.Application.Catalog.Promotions.Create;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;

namespace PlanelyApp.Application.Tests.Catalog;

public class CreatePromotionTests
{
    private readonly IPromotionRepository _promotions = Substitute.For<IPromotionRepository>();
    private readonly IBranchRepository _branches = Substitute.For<IBranchRepository>();
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public async Task A_per_service_promotion_for_all_branches_should_be_added_to_the_company_of_the_request()
    {
        var haircut = CatalogData.NewFixedService(_companyId);
        var added = CaptureAdded();

        var result = await Handler(PromotionData.ServicesListing(haircut)).HandleAsync(
            NewCommand([haircut.Id]) with { Name = "  Black Friday ", Description = " Weekend offer " },
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var promotion = added()!;
        result.Value.ShouldBe(promotion.Id);
        promotion.CompanyId.ShouldBe(_companyId);
        promotion.Kind.ShouldBe(PromotionKind.PerService);
        promotion.Name.ShouldBe("Black Friday");
        promotion.Description.ShouldBe("Weekend offer");
        promotion.Discount.ShouldBe(Discount.Percentage(10m));
        promotion.Validity.ShouldBe(PromotionData.November);
        promotion.AppliesToAllBranches.ShouldBeTrue();
        promotion.BranchIds.ShouldBeEmpty();
        promotion.ServiceIds.ShouldBe([haircut.Id], ignoreOrder: false);
        promotion.Weekdays.ShouldBeEmpty();
        promotion.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task A_bundle_for_some_branches_should_keep_its_branches_and_services()
    {
        var haircut = CatalogData.NewFixedService(_companyId, "Haircut");
        var beard = CatalogData.NewFixedService(_companyId, "Beard");
        var (branchA, branchB) = (Guid.NewGuid(), Guid.NewGuid());
        _branches.CountByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(2);
        var added = CaptureAdded();

        var result = await Handler(PromotionData.ServicesListing(haircut, beard)).HandleAsync(
            NewCommand(
                [haircut.Id, beard.Id],
                PromotionKind.Bundle,
                allBranches: false,
                branchIds: [branchA, branchB],
                discount: new DiscountData(DiscountType.FixedAmount, 15m)),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var promotion = added()!;
        promotion.Kind.ShouldBe(PromotionKind.Bundle);
        promotion.Discount.ShouldBe(Discount.FixedAmount(Money.From(15m)));
        promotion.AppliesToAllBranches.ShouldBeFalse();
        promotion.BranchIds.ShouldBe([branchA, branchB], ignoreOrder: false);
        promotion.ServiceIds.ShouldBe([haircut.Id, beard.Id], ignoreOrder: false);
    }

    [Fact]
    public async Task The_services_should_keep_the_order_of_the_request_and_repeated_ids_count_once()
    {
        var haircut = CatalogData.NewFixedService(_companyId, "Haircut");
        var beard = CatalogData.NewFixedService(_companyId, "Beard");
        var added = CaptureAdded();

        // The store answers in its own order.
        var result = await Handler(PromotionData.ServicesListing(beard, haircut)).HandleAsync(
            NewCommand([haircut.Id, beard.Id, haircut.Id]), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        added()!.ServiceIds.ShouldBe([haircut.Id, beard.Id], ignoreOrder: false);
    }

    [Fact]
    public async Task The_weekdays_should_be_kept_in_order()
    {
        var haircut = CatalogData.NewFixedService(_companyId);
        var added = CaptureAdded();

        var result = await Handler(PromotionData.ServicesListing(haircut)).HandleAsync(
            NewCommand([haircut.Id]) with { Weekdays = [DayOfWeek.Saturday, DayOfWeek.Monday] },
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        added()!.Weekdays.ShouldBe([DayOfWeek.Monday, DayOfWeek.Saturday], ignoreOrder: false);
    }

    [Fact]
    public async Task A_service_that_does_not_exist_should_be_not_found_and_add_nothing()
    {
        var haircut = CatalogData.NewFixedService(_companyId);

        var result = await Handler(PromotionData.ServicesListing(haircut)).HandleAsync(
            NewCommand([haircut.Id, Guid.NewGuid()]), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("ServiceOffering.NotFound");
        _promotions.DidNotReceive().Add(Arg.Any<Promotion>());
    }

    [Fact]
    public async Task A_branch_that_does_not_exist_should_be_not_found_and_add_nothing()
    {
        var haircut = CatalogData.NewFixedService(_companyId);
        _branches.CountByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(1);

        var result = await Handler(PromotionData.ServicesListing(haircut)).HandleAsync(
            NewCommand([haircut.Id], allBranches: false, branchIds: [Guid.NewGuid(), Guid.NewGuid()]),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Branch.NotFound");
        _promotions.DidNotReceive().Add(Arg.Any<Promotion>());
    }

    [Fact]
    public async Task A_promotion_for_all_branches_should_not_look_for_branches()
    {
        var haircut = CatalogData.NewFixedService(_companyId);

        await Handler(PromotionData.ServicesListing(haircut)).HandleAsync(
            NewCommand([haircut.Id]), TestContext.Current.CancellationToken);

        await _branches.DidNotReceive().CountByIdsAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_service_on_request_should_be_refused_by_the_domain_and_add_nothing()
    {
        var tattoo = CatalogData.NewOnRequestService(_companyId);

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(PromotionData.ServicesListing(tattoo))
            .HandleAsync(NewCommand([tattoo.Id]), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Promotion.OnRequestServiceNotAllowed");
        _promotions.DidNotReceive().Add(Arg.Any<Promotion>());
    }

    [Fact]
    public async Task A_bundle_with_one_service_should_be_refused_by_the_domain()
    {
        var haircut = CatalogData.NewFixedService(_companyId);

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(PromotionData.ServicesListing(haircut))
            .HandleAsync(NewCommand([haircut.Id], PromotionKind.Bundle), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Promotion.BundleRequiresAtLeastTwoServices");
        _promotions.DidNotReceive().Add(Arg.Any<Promotion>());
    }

    [Theory]
    [InlineData(DiscountType.Percentage, 0, "Discount.PercentageInvalid")]
    [InlineData(DiscountType.Percentage, 101, "Discount.PercentageInvalid")]
    [InlineData(DiscountType.FixedAmount, 0, "Discount.FixedAmountInvalid")]
    public async Task A_discount_the_domain_refuses_should_be_refused_and_add_nothing(
        DiscountType type, int value, string code)
    {
        var haircut = CatalogData.NewFixedService(_companyId);

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(PromotionData.ServicesListing(haircut))
            .HandleAsync(
                NewCommand([haircut.Id], discount: new DiscountData(type, value)),
                TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(code);
        _promotions.DidNotReceive().Add(Arg.Any<Promotion>());
    }

    [Fact]
    public async Task A_period_that_ends_before_it_starts_should_be_refused_by_the_domain()
    {
        var haircut = CatalogData.NewFixedService(_companyId);

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(PromotionData.ServicesListing(haircut))
            .HandleAsync(
                NewCommand([haircut.Id]) with { ValidUntil = PromotionData.Start.AddDays(-1) },
                TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("ValidityPeriod.EndBeforeStart");
        _promotions.DidNotReceive().Add(Arg.Any<Promotion>());
    }

    [Fact]
    public async Task A_weekday_that_does_not_exist_should_be_refused_by_the_domain_and_add_nothing()
    {
        var haircut = CatalogData.NewFixedService(_companyId);

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(PromotionData.ServicesListing(haircut))
            .HandleAsync(
                NewCommand([haircut.Id]) with { Weekdays = [(DayOfWeek)9] },
                TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Promotion.WeekdayInvalid");
        _promotions.DidNotReceive().Add(Arg.Any<Promotion>());
    }

    [Fact]
    public async Task A_valid_command_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new CreatePromotionValidator(),
            NewCommand([Guid.NewGuid()], allBranches: false, branchIds: [Guid.NewGuid()]));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task What_is_missing_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new CreatePromotionValidator(),
            NewCommand([Guid.NewGuid()]) with { Name = "", Discount = null!, ServiceIds = null! });

        problems.ShouldBe(
            ["Name:Validation.Required", "Discount:Validation.Required", "ServiceIds:Validation.Required"],
            ignoreOrder: true);
    }

    [Fact]
    public async Task A_kind_that_does_not_exist_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new CreatePromotionValidator(), NewCommand([Guid.NewGuid()], (PromotionKind)99));

        problems.ShouldBe(["Kind:Validation.Invalid"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_discount_type_that_does_not_exist_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new CreatePromotionValidator(),
            NewCommand([Guid.NewGuid()], discount: new DiscountData((DiscountType)99, 10m)));

        problems.ShouldBe(["Discount.Type:Validation.Invalid"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_discount_with_three_decimals_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new CreatePromotionValidator(),
            NewCommand([Guid.NewGuid()], discount: new DiscountData(DiscountType.Percentage, 10.123m)));

        problems.ShouldBe(["Discount.Value:Validation.Invalid"], ignoreOrder: true);
    }

    [Fact]
    public async Task An_empty_service_id_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new CreatePromotionValidator(), NewCommand([Guid.NewGuid(), Guid.Empty]));

        problems.ShouldBe(["ServiceIds[1]:Validation.Invalid"], ignoreOrder: true);
    }

    [Fact]
    public async Task All_branches_together_with_a_list_of_branches_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new CreatePromotionValidator(),
            NewCommand([Guid.NewGuid()], allBranches: true, branchIds: [Guid.NewGuid()]));

        problems.ShouldBe(["BranchIds:Validation.Invalid"], ignoreOrder: true);
    }

    [Fact]
    public async Task Some_branches_without_a_list_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new CreatePromotionValidator(), NewCommand([Guid.NewGuid()], allBranches: false));

        problems.ShouldBe(["BranchIds:Validation.Required"], ignoreOrder: true);
    }

    [Fact]
    public async Task An_empty_branch_id_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new CreatePromotionValidator(),
            NewCommand([Guid.NewGuid()], allBranches: false, branchIds: [Guid.Empty]));

        problems.ShouldBe(["BranchIds[0]:Validation.Invalid"], ignoreOrder: true);
    }

    private static CreatePromotionCommand NewCommand(
        IReadOnlyList<Guid> serviceIds,
        PromotionKind kind = PromotionKind.PerService,
        bool allBranches = true,
        IReadOnlyList<Guid>? branchIds = null,
        DiscountData? discount = null) =>
        new(
            kind,
            "Black Friday",
            discount ?? new DiscountData(DiscountType.Percentage, 10m),
            PromotionData.Start,
            PromotionData.End,
            serviceIds,
            allBranches,
            branchIds);

    private CreatePromotionHandler Handler(IServiceOfferingRepository services) =>
        new(_promotions, services, _branches, BranchData.TenantOf(_companyId));

    private Func<Promotion?> CaptureAdded()
    {
        Promotion? added = null;
        _promotions.When(repository => repository.Add(Arg.Any<Promotion>()))
            .Do(call => added = call.Arg<Promotion>());

        return () => added;
    }
}