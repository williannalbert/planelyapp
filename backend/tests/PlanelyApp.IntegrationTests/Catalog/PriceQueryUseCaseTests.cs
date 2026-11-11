using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.BranchOfferings.SetAvailability;
using PlanelyApp.Application.Catalog.BranchOfferings.SetPrice;
using PlanelyApp.Application.Catalog.Pricing;
using PlanelyApp.Application.Catalog.Pricing.GetPrice;
using PlanelyApp.Application.Catalog.Promotions;
using PlanelyApp.Application.Catalog.Promotions.Create;
using PlanelyApp.Application.Catalog.Promotions.End;
using PlanelyApp.Application.Catalog.ServiceOfferings;
using PlanelyApp.Application.Catalog.ServiceOfferings.Create;
using PlanelyApp.Application.Catalog.ServiceOfferings.Deactivate;
using PlanelyApp.Application.Organization.Branches.Create;
using PlanelyApp.Application.Organization.Shared;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Catalog;

/// <summary>The whole way, as a request has it: dispatcher, decorators, handler, repositories, calculator and MySQL.</summary>
public class PriceQueryUseCaseTests(MySqlFixture mysql)
{
    // November 20, 2027 is a Saturday. The branches are in São Paulo (UTC-3).
    private static readonly DateOnly Start = new(2027, 11, 20);
    private static readonly DateOnly End = new(2027, 11, 30);
    private static readonly DateTime Monday = new(2027, 11, 22, 13, 0, 0, DateTimeKind.Utc);
    private static readonly DiscountData TenPercent = new(DiscountType.Percentage, 10m);

    [Fact]
    public async Task Without_promotions_the_prices_and_the_totals_should_come_from_the_catalog()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SeedAsync(cancellationToken);

        var quote = await PriceAsync(
            scenario.Company, scenario.Centro, Monday, [scenario.Haircut, scenario.Beard, scenario.Tattoo], cancellationToken);

        quote.Lines.Select(line => line.ServiceName).ShouldBe(["Haircut", "Beard", "Tattoo"], ignoreOrder: false);
        quote.Lines[0].ListPrice.ShouldBe(50m);
        quote.Lines[0].FinalPrice.ShouldBe(50m);
        quote.Lines[1].ListPrice.ShouldBe(30m);
        quote.Lines[2].IsOnRequest.ShouldBeTrue();
        quote.Lines[2].ListPrice.ShouldBeNull();
        quote.ListTotal.ShouldBe(80m);
        quote.DiscountTotal.ShouldBe(0m);
        quote.FinalTotal.ShouldBe(80m);
        quote.HasServiceOnRequest.ShouldBeTrue();
    }

    [Fact]
    public async Task The_price_of_a_branch_should_apply_only_in_that_branch()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SeedAsync(cancellationToken);
        await mysql.SendAsync(
            scenario.Company, new SetBranchOfferingPriceCommand(scenario.Centro, scenario.Haircut, 40m), cancellationToken);

        var centro = await PriceAsync(scenario.Company, scenario.Centro, Monday, [scenario.Haircut], cancellationToken);
        var norte = await PriceAsync(scenario.Company, scenario.Norte, Monday, [scenario.Haircut], cancellationToken);

        centro.Lines.Single().ListPrice.ShouldBe(40m);
        norte.Lines.Single().ListPrice.ShouldBe(50m);
    }

    [Fact]
    public async Task A_promotion_should_follow_the_local_day_of_the_branch()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SeedAsync(cancellationToken);
        var promotion = await CreatePromotionAsync(
            scenario.Company, "Black Friday", PromotionKind.PerService, [scenario.Haircut], TenPercent, cancellationToken);

        var inside = await DiscountOfAsync(scenario, new DateTime(2027, 11, 22, 13, 0, 0, DateTimeKind.Utc), cancellationToken);
        var lastNight = await DiscountOfAsync(scenario, new DateTime(2027, 12, 1, 2, 0, 0, DateTimeKind.Utc), cancellationToken); // Nov 30, 23:00
        var midnight = await DiscountOfAsync(scenario, new DateTime(2027, 12, 1, 3, 0, 0, DateTimeKind.Utc), cancellationToken);  // Dec 1, 00:00
        var beforeStart = await DiscountOfAsync(scenario, new DateTime(2027, 11, 20, 2, 0, 0, DateTimeKind.Utc), cancellationToken); // Nov 19, 23:00

        inside.Discount.ShouldBe(5m);
        inside.PromotionId.ShouldBe(promotion);
        lastNight.Discount.ShouldBe(5m);
        midnight.Discount.ShouldBe(0m);
        beforeStart.Discount.ShouldBe(0m);
    }

    [Fact]
    public async Task A_promotion_for_some_weekdays_should_apply_only_on_them()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SeedAsync(cancellationToken);
        await CreatePromotionAsync(
            scenario.Company, "Saturdays", PromotionKind.PerService, [scenario.Haircut], TenPercent, cancellationToken,
            weekdays: [DayOfWeek.Saturday]);

        var onMonday = await DiscountOfAsync(scenario, Monday, cancellationToken);
        var onSaturday = await DiscountOfAsync(
            scenario, new DateTime(2027, 11, 20, 15, 0, 0, DateTimeKind.Utc), cancellationToken);

        onMonday.Discount.ShouldBe(0m);
        onSaturday.Discount.ShouldBe(5m);
    }

    [Fact]
    public async Task The_best_of_two_promotions_should_win()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SeedAsync(cancellationToken);
        await CreatePromotionAsync(
            scenario.Company, "Ten percent", PromotionKind.PerService, [scenario.Haircut], TenPercent, cancellationToken);
        var eightOff = await CreatePromotionAsync(
            scenario.Company, "Eight off", PromotionKind.PerService, [scenario.Haircut],
            new DiscountData(DiscountType.FixedAmount, 8m), cancellationToken);

        var line = await DiscountOfAsync(scenario, Monday, cancellationToken);

        line.Discount.ShouldBe(8m);
        line.FinalPrice.ShouldBe(42m);
        line.PromotionId.ShouldBe(eightOff);
        line.PromotionName.ShouldBe("Eight off");
    }

    [Fact]
    public async Task A_bundle_should_apply_only_when_all_its_services_are_in_the_appointment()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SeedAsync(cancellationToken);
        var combo = await CreatePromotionAsync(
            scenario.Company, "Combo", PromotionKind.Bundle, [scenario.Haircut, scenario.Beard],
            new DiscountData(DiscountType.FixedAmount, 15m), cancellationToken);

        var both = await PriceAsync(
            scenario.Company, scenario.Centro, Monday, [scenario.Haircut, scenario.Beard], cancellationToken);
        var onlyOne = await PriceAsync(scenario.Company, scenario.Centro, Monday, [scenario.Haircut], cancellationToken);

        both.Lines.ShouldAllBe(line => line.IsFromBundle && line.PromotionId == combo && line.PromotionName == "Combo");
        both.Lines.Sum(line => line.Discount).ShouldBe(15m);
        both.DiscountTotal.ShouldBe(15m);
        both.FinalTotal.ShouldBe(65m);
        onlyOne.Lines.Single().Discount.ShouldBe(0m);
        onlyOne.Lines.Single().IsFromBundle.ShouldBeFalse();
    }

    [Fact]
    public async Task Promotions_that_ended_or_belong_to_another_branch_or_company_should_not_apply()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SeedAsync(cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var theirHaircut = await CreateServiceAsync(other.Id, "Haircut", 50m, cancellationToken);
        var ended = await CreatePromotionAsync(
            scenario.Company, "Ended", PromotionKind.PerService, [scenario.Haircut], TenPercent, cancellationToken);
        await mysql.SendAsync(scenario.Company, new EndPromotionCommand(ended), cancellationToken);
        await CreatePromotionAsync(
            scenario.Company, "Norte only", PromotionKind.PerService, [scenario.Haircut], TenPercent, cancellationToken,
            branches: [scenario.Norte]);
        await CreatePromotionAsync(
            other.Id, "Another company", PromotionKind.PerService, [theirHaircut], TenPercent, cancellationToken);

        var inCentro = await DiscountOfAsync(scenario, Monday, cancellationToken);
        var inNorte = (await PriceAsync(
            scenario.Company, scenario.Norte, Monday, [scenario.Haircut], cancellationToken)).Lines.Single();

        inCentro.Discount.ShouldBe(0m);
        inNorte.Discount.ShouldBe(5m);
        inNorte.PromotionName.ShouldBe("Norte only");
    }

    [Fact]
    public async Task An_inactive_service_or_one_the_branch_does_not_offer_should_be_priced_but_not_available()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SeedAsync(cancellationToken);
        await mysql.SendAsync(scenario.Company, new DeactivateServiceOfferingCommand(scenario.Haircut), cancellationToken);
        await mysql.SendAsync(
            scenario.Company, new SetBranchOfferingAvailabilityCommand(scenario.Centro, scenario.Beard, false), cancellationToken);

        var centro = await PriceAsync(
            scenario.Company, scenario.Centro, Monday, [scenario.Haircut, scenario.Beard, scenario.Tattoo], cancellationToken);
        var norte = await PriceAsync(scenario.Company, scenario.Norte, Monday, [scenario.Beard], cancellationToken);

        centro.Lines[0].IsAvailable.ShouldBeFalse();
        centro.Lines[0].ListPrice.ShouldBe(50m);
        centro.Lines[1].IsAvailable.ShouldBeFalse();
        centro.Lines[1].ListPrice.ShouldBe(30m);
        centro.Lines[2].IsAvailable.ShouldBeTrue();
        norte.Lines.Single().IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task A_branch_or_service_that_is_not_in_the_company_and_a_time_that_is_not_utc_should_be_refused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SeedAsync(cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var theirBranch = await CreateBranchAsync(other.Id, "Centro", cancellationToken);
        var theirService = await CreateServiceAsync(other.Id, "Haircut", 50m, cancellationToken);

        var unknownBranch = await mysql.QueryAsync(
            scenario.Company, new GetPriceQuery(Guid.CreateVersion7(), Monday, [scenario.Haircut]), cancellationToken);
        var anotherBranch = await mysql.QueryAsync(
            scenario.Company, new GetPriceQuery(theirBranch, Monday, [scenario.Haircut]), cancellationToken);
        var unknownService = await mysql.QueryAsync(
            scenario.Company, new GetPriceQuery(scenario.Centro, Monday, [Guid.CreateVersion7()]), cancellationToken);
        var anotherService = await mysql.QueryAsync(
            scenario.Company, new GetPriceQuery(scenario.Centro, Monday, [scenario.Haircut, theirService]), cancellationToken);
        var notUtc = await Should.ThrowAsync<DomainException>(() => mysql.QueryAsync(
            scenario.Company,
            new GetPriceQuery(scenario.Centro, new DateTime(2027, 11, 22, 13, 0, 0), [scenario.Haircut]),
            cancellationToken));

        unknownBranch.Error.Type.ShouldBe(ErrorType.NotFound);
        unknownBranch.Error.Code.ShouldBe("Branch.NotFound");
        anotherBranch.Error.Code.ShouldBe("Branch.NotFound");
        unknownService.Error.Type.ShouldBe(ErrorType.NotFound);
        unknownService.Error.Code.ShouldBe("ServiceOffering.NotFound");
        anotherService.Error.Code.ShouldBe("ServiceOffering.NotFound");
        notUtc.Code.ShouldBe("Branch.TimeMustBeUtc");
    }

    private async Task<PriceLineDto> DiscountOfAsync(Scenario scenario, DateTime start, CancellationToken cancellationToken) =>
        (await PriceAsync(scenario.Company, scenario.Centro, start, [scenario.Haircut], cancellationToken)).Lines.Single();

    private async Task<PriceQuoteDto> PriceAsync(
        Guid companyId,
        Guid branchId,
        DateTime startUtc,
        Guid[] serviceIds,
        CancellationToken cancellationToken) =>
        (await mysql.QueryAsync(companyId, new GetPriceQuery(branchId, startUtc, serviceIds), cancellationToken)).Value;

    private async Task<Scenario> SeedAsync(CancellationToken cancellationToken)
    {
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        return new Scenario(
            company.Id,
            await CreateBranchAsync(company.Id, "Centro", cancellationToken),
            await CreateBranchAsync(company.Id, "Norte", cancellationToken),
            await CreateServiceAsync(company.Id, "Haircut", 50m, cancellationToken),
            await CreateServiceAsync(company.Id, "Beard", 30m, cancellationToken),
            await CreateServiceAsync(company.Id, "Tattoo", null, cancellationToken));
    }

    private async Task<Guid> CreatePromotionAsync(
        Guid companyId,
        string name,
        PromotionKind kind,
        Guid[] serviceIds,
        DiscountData discount,
        CancellationToken cancellationToken,
        Guid[]? branches = null,
        DayOfWeek[]? weekdays = null) =>
        (await mysql.SendAsync(
            companyId,
            new CreatePromotionCommand(
                kind, name, discount, Start, End, serviceIds, branches is null, branches, weekdays),
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

    private sealed record Scenario(Guid Company, Guid Centro, Guid Norte, Guid Haircut, Guid Beard, Guid Tattoo);
}