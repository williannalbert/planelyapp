using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.Promotions;
using PlanelyApp.Application.Catalog.Promotions.Create;
using PlanelyApp.Application.Catalog.Promotions.End;
using PlanelyApp.Application.Catalog.Promotions.Get;
using PlanelyApp.Application.Catalog.Promotions.List;
using PlanelyApp.Application.Catalog.ServiceOfferings;
using PlanelyApp.Application.Catalog.ServiceOfferings.Create;
using PlanelyApp.Application.Organization.Branches.Create;
using PlanelyApp.Application.Organization.Shared;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Catalog;

/// <summary>The whole way, as a request has it: dispatcher, decorators, handler, repository or read model, and MySQL.</summary>
public class PromotionUseCaseTests(MySqlFixture mysql)
{
    private static readonly DateOnly Start = new(2027, 11, 20);
    private static readonly DateOnly End = new(2027, 11, 30);

    [Fact]
    public async Task A_per_service_promotion_for_some_branches_should_be_saved_normalized_and_read_back()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var haircut = await CreateServiceAsync(company.Id, "Haircut", 50m, cancellationToken);
        var beard = await CreateServiceAsync(company.Id, "Beard", 30m, cancellationToken);
        var centro = await CreateBranchAsync(company.Id, "Centro", cancellationToken);

        var created = await mysql.SendAsync(
            company.Id,
            new CreatePromotionCommand(
                PromotionKind.PerService,
                "  Black Friday ",
                new DiscountData(DiscountType.Percentage, 12.5m),
                Start,
                End,
                [beard, haircut],
                false,
                [centro],
                [DayOfWeek.Saturday, DayOfWeek.Monday],
                " Weekend offer "),
            cancellationToken);

        created.IsSuccess.ShouldBeTrue();
        var dto = await GetAsync(company.Id, created.Value, cancellationToken);
        dto.Id.ShouldBe(created.Value);
        dto.Kind.ShouldBe(PromotionKind.PerService);
        dto.Name.ShouldBe("Black Friday");
        dto.Description.ShouldBe("Weekend offer");
        dto.Discount.ShouldBe(new DiscountData(DiscountType.Percentage, 12.5m));
        dto.ValidFrom.ShouldBe(Start);
        dto.ValidUntil.ShouldBe(End);
        dto.Weekdays.ShouldBe([DayOfWeek.Monday, DayOfWeek.Saturday], ignoreOrder: false);
        dto.AppliesToAllBranches.ShouldBeFalse();
        dto.BranchIds.ShouldBe([centro], ignoreOrder: false);
        dto.ServiceIds.ShouldBe([beard, haircut], ignoreOrder: false);
        dto.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task A_bundle_for_all_branches_should_be_saved_and_read_back()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var haircut = await CreateServiceAsync(company.Id, "Haircut", 50m, cancellationToken);
        var beard = await CreateServiceAsync(company.Id, "Beard", 30m, cancellationToken);

        var created = await mysql.SendAsync(
            company.Id,
            new CreatePromotionCommand(
                PromotionKind.Bundle,
                "Cut and beard",
                new DiscountData(DiscountType.FixedAmount, 15m),
                Start,
                End,
                [haircut, beard],
                true),
            cancellationToken);

        created.IsSuccess.ShouldBeTrue();
        var dto = await GetAsync(company.Id, created.Value, cancellationToken);
        dto.Kind.ShouldBe(PromotionKind.Bundle);
        dto.Discount.ShouldBe(new DiscountData(DiscountType.FixedAmount, 15m));
        dto.AppliesToAllBranches.ShouldBeTrue();
        dto.BranchIds.ShouldBeEmpty();
        dto.Weekdays.ShouldBeEmpty();
        dto.ServiceIds.ShouldBe([haircut, beard], ignoreOrder: false);
    }

    [Fact]
    public async Task A_service_of_another_company_should_be_not_found()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var theirs = await CreateServiceAsync(other.Id, "Haircut", 50m, cancellationToken);

        var result = await mysql.SendAsync(
            company.Id, NewPromotion(PromotionKind.PerService, [theirs]), cancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("ServiceOffering.NotFound");
        (await ListAsync(company.Id, new ListPromotionsQuery(), cancellationToken)).TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task A_branch_that_does_not_exist_or_belongs_to_another_company_should_be_not_found()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var haircut = await CreateServiceAsync(company.Id, "Haircut", 50m, cancellationToken);
        var mine = await CreateBranchAsync(company.Id, "Centro", cancellationToken);
        var theirs = await CreateBranchAsync(other.Id, "Centro", cancellationToken);
        var command = NewPromotion(PromotionKind.PerService, [haircut]) with { AppliesToAllBranches = false };

        var unknown = await mysql.SendAsync(
            company.Id, command with { BranchIds = [mine, Guid.CreateVersion7()] }, cancellationToken);
        var another = await mysql.SendAsync(
            company.Id, command with { BranchIds = [mine, theirs] }, cancellationToken);

        unknown.Error.Code.ShouldBe("Branch.NotFound");
        another.Error.Type.ShouldBe(ErrorType.NotFound);
        another.Error.Code.ShouldBe("Branch.NotFound");
        (await ListAsync(company.Id, new ListPromotionsQuery(), cancellationToken)).TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task A_service_on_request_should_be_refused_by_the_domain_and_nothing_should_be_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var tattoo = await CreateServiceAsync(company.Id, "Tattoo", null, cancellationToken);

        var exception = await Should.ThrowAsync<DomainException>(() => mysql.SendAsync(
            company.Id, NewPromotion(PromotionKind.PerService, [tattoo]), cancellationToken));

        exception.Code.ShouldBe("Promotion.OnRequestServiceNotAllowed");
        (await ListAsync(company.Id, new ListPromotionsQuery(), cancellationToken)).TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Ending_should_turn_the_promotion_off_and_keep_it()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var haircut = await CreateServiceAsync(company.Id, "Haircut", 50m, cancellationToken);
        var promotion = await CreatePromotionAsync(
            company.Id, "Black Friday", PromotionKind.PerService, [haircut], Start, End, cancellationToken);

        var ended = await mysql.SendAsync(company.Id, new EndPromotionCommand(promotion), cancellationToken);
        var endedAgain = await mysql.SendAsync(company.Id, new EndPromotionCommand(promotion), cancellationToken);

        ended.IsSuccess.ShouldBeTrue();
        endedAgain.IsSuccess.ShouldBeTrue();
        (await GetAsync(company.Id, promotion, cancellationToken)).IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task A_promotion_of_another_company_should_be_not_found_to_get_and_to_end()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var haircut = await CreateServiceAsync(company.Id, "Haircut", 50m, cancellationToken);
        var promotion = await CreatePromotionAsync(
            company.Id, "Black Friday", PromotionKind.PerService, [haircut], Start, End, cancellationToken);

        var get = await mysql.QueryAsync(other.Id, new GetPromotionQuery(promotion), cancellationToken);
        var end = await mysql.SendAsync(other.Id, new EndPromotionCommand(promotion), cancellationToken);
        var missing = await mysql.QueryAsync(company.Id, new GetPromotionQuery(Guid.CreateVersion7()), cancellationToken);

        get.Error.Code.ShouldBe("Promotion.NotFound");
        end.Error.Type.ShouldBe(ErrorType.NotFound);
        end.Error.Code.ShouldBe("Promotion.NotFound");
        missing.Error.Code.ShouldBe("Promotion.NotFound");
        (await GetAsync(company.Id, promotion, cancellationToken)).IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task The_list_should_filter_search_and_page_only_inside_the_company()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var haircut = await CreateServiceAsync(company.Id, "Haircut", 50m, cancellationToken);
        var beard = await CreateServiceAsync(company.Id, "Beard", 30m, cancellationToken);
        var theirs = await CreateServiceAsync(other.Id, "Haircut", 50m, cancellationToken);
        var winterStart = new DateOnly(2028, 6, 1);
        await CreatePromotionAsync(
            company.Id, "Summer sale", PromotionKind.PerService, [haircut], Start, End, cancellationToken);
        await CreatePromotionAsync(
            company.Id, "Winter sale", PromotionKind.PerService, [haircut], winterStart, new DateOnly(2028, 6, 30), cancellationToken);
        await CreatePromotionAsync(
            company.Id, "Combo cut and beard", PromotionKind.Bundle, [haircut, beard], Start, End, cancellationToken,
            new DiscountData(DiscountType.FixedAmount, 15m));
        var old = await CreatePromotionAsync(
            company.Id, "Old offer", PromotionKind.PerService, [beard], Start, End, cancellationToken);
        await mysql.SendAsync(company.Id, new EndPromotionCommand(old), cancellationToken);
        await CreatePromotionAsync(
            other.Id, "Summer sale", PromotionKind.PerService, [theirs], Start, End, cancellationToken);

        var all = await ListAsync(company.Id, new ListPromotionsQuery(), cancellationToken);
        var sales = await ListAsync(company.Id, new ListPromotionsQuery("  SALE "), cancellationToken);
        var bundles = await ListAsync(company.Id, new ListPromotionsQuery(Kind: PromotionKind.Bundle), cancellationToken);
        var ended = await ListAsync(company.Id, new ListPromotionsQuery(IsActive: false), cancellationToken);
        var inNovember = await ListAsync(
            company.Id, new ListPromotionsQuery(ValidOn: new DateOnly(2027, 11, 25)), cancellationToken);
        var inWinter = await ListAsync(
            company.Id, new ListPromotionsQuery(ValidOn: new DateOnly(2028, 6, 10)), cancellationToken);
        var secondPage = await ListAsync(company.Id, new ListPromotionsQuery(Page: 2, PageSize: 2), cancellationToken);

        // The ones that start latest come first, then by name.
        Names(all).ShouldBe(["Winter sale", "Combo cut and beard", "Old offer", "Summer sale"], ignoreOrder: false);
        all.TotalCount.ShouldBe(4);
        Names(sales).ShouldBe(["Winter sale", "Summer sale"], ignoreOrder: false);
        Names(bundles).ShouldBe(["Combo cut and beard"], ignoreOrder: false);
        Names(ended).ShouldBe(["Old offer"], ignoreOrder: false);
        Names(inNovember).ShouldBe(["Combo cut and beard", "Old offer", "Summer sale"], ignoreOrder: false);
        Names(inWinter).ShouldBe(["Winter sale"], ignoreOrder: false);
        Names(secondPage).ShouldBe(["Old offer", "Summer sale"], ignoreOrder: false);
        secondPage.TotalCount.ShouldBe(4);

        var combo = all.Items.Single(item => item.Name == "Combo cut and beard");
        combo.Kind.ShouldBe(PromotionKind.Bundle);
        combo.DiscountType.ShouldBe(DiscountType.FixedAmount);
        combo.DiscountValue.ShouldBe(15m);
        combo.ValidFrom.ShouldBe(Start);
        combo.ValidUntil.ShouldBe(End);
        combo.AppliesToAllBranches.ShouldBeTrue();
        combo.ServiceCount.ShouldBe(2);
        combo.IsActive.ShouldBeTrue();
        all.Items.Single(item => item.Name == "Summer sale").ServiceCount.ShouldBe(1);
        (await ListAsync(other.Id, new ListPromotionsQuery(), cancellationToken)).TotalCount.ShouldBe(1);
    }

    private static CreatePromotionCommand NewPromotion(PromotionKind kind, IReadOnlyList<Guid> serviceIds) =>
        new(kind, "Black Friday", new DiscountData(DiscountType.Percentage, 10m), Start, End, serviceIds, true);

    private async Task<Guid> CreatePromotionAsync(
        Guid companyId,
        string name,
        PromotionKind kind,
        IReadOnlyList<Guid> serviceIds,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken,
        DiscountData? discount = null) =>
        (await mysql.SendAsync(
            companyId,
            new CreatePromotionCommand(
                kind, name, discount ?? new DiscountData(DiscountType.Percentage, 10m), start, end, serviceIds, true),
            cancellationToken)).Value;

    private async Task<Guid> CreateServiceAsync(
        Guid companyId,
        string name,
        decimal? price,
        CancellationToken cancellationToken) =>
        (await mysql.SendAsync(
            companyId,
            new CreateServiceOfferingCommand(
                name, 30, new PricingData(price is null ? PricingType.OnRequest : PricingType.Fixed, price)),
            cancellationToken)).Value;

    private async Task<Guid> CreateBranchAsync(Guid companyId, string name, CancellationToken cancellationToken) =>
        (await mysql.SendAsync(
            companyId,
            new CreateBranchCommand(
                name,
                new AddressData("Rua das Flores", "123", "Sala 4", "Centro", "São Paulo", "SP", "01310100"),
                "+551133334444",
                "America/Sao_Paulo"),
            cancellationToken)).Value;

    private async Task<PromotionDto> GetAsync(Guid companyId, Guid promotionId, CancellationToken cancellationToken) =>
        (await mysql.QueryAsync(companyId, new GetPromotionQuery(promotionId), cancellationToken)).Value;

    private async Task<PagedResult<PromotionSummaryDto>> ListAsync(
        Guid companyId,
        ListPromotionsQuery query,
        CancellationToken cancellationToken) =>
        (await mysql.QueryAsync(companyId, query, cancellationToken)).Value;

    private static string[] Names(PagedResult<PromotionSummaryDto> page) =>
        page.Items.Select(item => item.Name).ToArray();
}