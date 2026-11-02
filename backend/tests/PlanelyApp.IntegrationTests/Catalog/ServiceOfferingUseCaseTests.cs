using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.ServiceCategories.Create;
using PlanelyApp.Application.Catalog.ServiceOfferings;
using PlanelyApp.Application.Catalog.ServiceOfferings.Activate;
using PlanelyApp.Application.Catalog.ServiceOfferings.ChangePricing;
using PlanelyApp.Application.Catalog.ServiceOfferings.Create;
using PlanelyApp.Application.Catalog.ServiceOfferings.Deactivate;
using PlanelyApp.Application.Catalog.ServiceOfferings.Get;
using PlanelyApp.Application.Catalog.ServiceOfferings.List;
using PlanelyApp.Application.Catalog.ServiceOfferings.UpdateDetails;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Catalog;

/// <summary>The whole way, as a request has it: dispatcher, decorators, handler, repository or read model, and MySQL.</summary>
public class ServiceOfferingUseCaseTests(MySqlFixture mysql)
{
    [Fact]
    public async Task A_fixed_price_service_should_be_saved_normalized_and_read_back()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var category = await CreateCategoryAsync(company.Id, "Hair", cancellationToken);

        var created = await mysql.SendAsync(
            company.Id,
            new CreateServiceOfferingCommand(
                "  Haircut ", 45, new PricingData(PricingType.Fixed, 59.9m), " Scissors and clipper ", category),
            cancellationToken);

        created.IsSuccess.ShouldBeTrue();
        var dto = await GetAsync(company.Id, created.Value, cancellationToken);
        dto.Id.ShouldBe(created.Value);
        dto.Name.ShouldBe("Haircut");
        dto.Description.ShouldBe("Scissors and clipper");
        dto.DurationMinutes.ShouldBe(45);
        dto.PricingType.ShouldBe(PricingType.Fixed);
        dto.BasePrice.ShouldBe(59.9m);
        dto.CategoryId.ShouldBe(category);
        dto.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task An_on_request_service_should_be_saved_without_a_price()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        var created = await mysql.SendAsync(
            company.Id,
            new CreateServiceOfferingCommand("Custom tattoo", 120, new PricingData(PricingType.OnRequest)),
            cancellationToken);

        created.IsSuccess.ShouldBeTrue();
        var dto = await GetAsync(company.Id, created.Value, cancellationToken);
        dto.PricingType.ShouldBe(PricingType.OnRequest);
        dto.BasePrice.ShouldBeNull();
        dto.Description.ShouldBeNull();
        dto.CategoryId.ShouldBeNull();
    }

    [Fact]
    public async Task A_name_should_be_unique_inside_the_company_but_free_in_another()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        await CreateServiceAsync(company.Id, "Haircut", cancellationToken);
        var command = new CreateServiceOfferingCommand("Haircut", 30, new PricingData(PricingType.Fixed, 10m));

        var same = await mysql.SendAsync(company.Id, command, cancellationToken);
        var otherCase = await mysql.SendAsync(company.Id, command with { Name = "HAIRCUT" }, cancellationToken);
        var inAnotherCompany = await mysql.SendAsync(other.Id, command, cancellationToken);

        same.Error.Type.ShouldBe(ErrorType.Conflict);
        same.Error.Code.ShouldBe("ServiceOffering.NameAlreadyExists");
        otherCase.Error.Code.ShouldBe("ServiceOffering.NameAlreadyExists");
        inAnotherCompany.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task A_category_of_another_company_should_not_be_found()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var foreign = await CreateCategoryAsync(other.Id, "Hair", cancellationToken);

        var result = await mysql.SendAsync(
            company.Id,
            new CreateServiceOfferingCommand("Haircut", 30, new PricingData(PricingType.Fixed, 50m), null, foreign),
            cancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("ServiceCategory.NotFound");
    }

    [Fact]
    public async Task The_details_should_be_replaced_and_the_category_taken_away_when_left_out()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var hair = await CreateCategoryAsync(company.Id, "Hair", cancellationToken);
        var beard = await CreateCategoryAsync(company.Id, "Beard", cancellationToken);
        var service = await CreateServiceAsync(company.Id, "Haircut", cancellationToken, categoryId: hair);

        var moved = await mysql.SendAsync(
            company.Id,
            new UpdateServiceOfferingDetailsCommand(service, "Beard trim", 20, " Quick ", beard),
            cancellationToken);
        var afterMove = await GetAsync(company.Id, service, cancellationToken);

        var cleared = await mysql.SendAsync(
            company.Id, new UpdateServiceOfferingDetailsCommand(service, "Beard trim", 20), cancellationToken);
        var afterClear = await GetAsync(company.Id, service, cancellationToken);

        moved.IsSuccess.ShouldBeTrue();
        afterMove.Name.ShouldBe("Beard trim");
        afterMove.Description.ShouldBe("Quick");
        afterMove.DurationMinutes.ShouldBe(20);
        afterMove.CategoryId.ShouldBe(beard);
        afterMove.BasePrice.ShouldBe(50m);
        cleared.IsSuccess.ShouldBeTrue();
        afterClear.CategoryId.ShouldBeNull();
        afterClear.Description.ShouldBeNull();
    }

    [Fact]
    public async Task A_name_should_clash_with_another_service_but_never_with_the_own_one()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        await CreateServiceAsync(company.Id, "Haircut", cancellationToken);
        var shave = await CreateServiceAsync(company.Id, "Shave", cancellationToken);

        var taken = await mysql.SendAsync(
            company.Id, new UpdateServiceOfferingDetailsCommand(shave, "HAIRCUT", 30), cancellationToken);
        var own = await mysql.SendAsync(
            company.Id, new UpdateServiceOfferingDetailsCommand(shave, "Shave", 15), cancellationToken);
        var ownCase = await mysql.SendAsync(
            company.Id, new UpdateServiceOfferingDetailsCommand(shave, "SHAVE", 15), cancellationToken);

        taken.Error.Code.ShouldBe("ServiceOffering.NameAlreadyExists");
        own.IsSuccess.ShouldBeTrue();
        ownCase.IsSuccess.ShouldBeTrue();
        (await GetAsync(company.Id, shave, cancellationToken)).Name.ShouldBe("SHAVE");
    }

    [Fact]
    public async Task The_pricing_should_change_both_ways_and_be_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var service = await CreateServiceAsync(company.Id, "Haircut", cancellationToken);

        var toRequest = await mysql.SendAsync(
            company.Id,
            new ChangeServiceOfferingPricingCommand(service, new PricingData(PricingType.OnRequest)),
            cancellationToken);
        var onRequest = await GetAsync(company.Id, service, cancellationToken);

        var toFixed = await mysql.SendAsync(
            company.Id,
            new ChangeServiceOfferingPricingCommand(service, new PricingData(PricingType.Fixed, 80m)),
            cancellationToken);
        var fixedAgain = await GetAsync(company.Id, service, cancellationToken);

        toRequest.IsSuccess.ShouldBeTrue();
        onRequest.PricingType.ShouldBe(PricingType.OnRequest);
        onRequest.BasePrice.ShouldBeNull();
        toFixed.IsSuccess.ShouldBeTrue();
        fixedAgain.PricingType.ShouldBe(PricingType.Fixed);
        fixedAgain.BasePrice.ShouldBe(80m);
    }

    [Fact]
    public async Task A_service_should_be_deactivated_and_activated()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var service = await CreateServiceAsync(company.Id, "Haircut", cancellationToken);

        await mysql.SendAsync(company.Id, new DeactivateServiceOfferingCommand(service), cancellationToken);
        var off = await GetAsync(company.Id, service, cancellationToken);
        await mysql.SendAsync(company.Id, new ActivateServiceOfferingCommand(service), cancellationToken);
        var on = await GetAsync(company.Id, service, cancellationToken);

        off.IsActive.ShouldBeFalse();
        on.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task The_list_should_search_filter_and_page_in_a_stable_order()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var hair = await CreateCategoryAsync(company.Id, "Hair", cancellationToken);
        var beard = await CreateCategoryAsync(company.Id, "Beard", cancellationToken);
        await CreateServiceAsync(company.Id, "Gamma color", cancellationToken, categoryId: hair);
        await CreateServiceAsync(company.Id, "Alpha cut", cancellationToken, categoryId: hair);
        await CreateServiceAsync(company.Id, "Beta shave", cancellationToken, price: null, categoryId: beard);
        await CreateServiceAsync(company.Id, "Delta wash", cancellationToken);
        var epsilon = await CreateServiceAsync(company.Id, "Epsilon spa", cancellationToken, categoryId: hair);
        await mysql.SendAsync(company.Id, new DeactivateServiceOfferingCommand(epsilon), cancellationToken);

        var all = await ListAsync(company.Id, new ListServiceOfferingsQuery(), cancellationToken);
        var secondPage = await ListAsync(company.Id, new ListServiceOfferingsQuery(Page: 2, PageSize: 2), cancellationToken);
        var inHair = await ListAsync(company.Id, new ListServiceOfferingsQuery(CategoryId: hair), cancellationToken);
        var active = await ListAsync(company.Id, new ListServiceOfferingsQuery(IsActive: true), cancellationToken);
        var inactive = await ListAsync(company.Id, new ListServiceOfferingsQuery(IsActive: false), cancellationToken);
        var searched = await ListAsync(company.Id, new ListServiceOfferingsQuery(Search: "SHAVE"), cancellationToken);
        var combined = await ListAsync(
            company.Id, new ListServiceOfferingsQuery(CategoryId: hair, IsActive: true), cancellationToken);

        Names(all).ShouldBe(["Alpha cut", "Beta shave", "Delta wash", "Epsilon spa", "Gamma color"], ignoreOrder: false);
        all.TotalCount.ShouldBe(5);
        all.TotalPages.ShouldBe(1);
        all.Items[0].CategoryName.ShouldBe("Hair");
        all.Items[0].PricingType.ShouldBe(PricingType.Fixed);
        all.Items[0].BasePrice.ShouldBe(50m);
        all.Items[0].DurationMinutes.ShouldBe(30);
        all.Items[1].CategoryName.ShouldBe("Beard");
        all.Items[1].PricingType.ShouldBe(PricingType.OnRequest);
        all.Items[1].BasePrice.ShouldBeNull();
        all.Items[2].CategoryId.ShouldBeNull();
        all.Items[2].CategoryName.ShouldBeNull();
        Names(secondPage).ShouldBe(["Delta wash", "Epsilon spa"], ignoreOrder: false);
        secondPage.TotalCount.ShouldBe(5);
        secondPage.TotalPages.ShouldBe(3);
        Names(inHair).ShouldBe(["Alpha cut", "Epsilon spa", "Gamma color"], ignoreOrder: false);
        Names(active).ShouldBe(["Alpha cut", "Beta shave", "Delta wash", "Gamma color"], ignoreOrder: false);
        Names(inactive).ShouldBe(["Epsilon spa"], ignoreOrder: false);
        Names(searched).ShouldBe(["Beta shave"], ignoreOrder: false);
        Names(combined).ShouldBe(["Alpha cut", "Gamma color"], ignoreOrder: false);
    }

    [Fact]
    public async Task Services_of_another_company_should_not_be_read_changed_or_listed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var service = await CreateServiceAsync(company.Id, "Haircut", cancellationToken);

        var get = await mysql.QueryAsync(other.Id, new GetServiceOfferingQuery(service), cancellationToken);
        var update = await mysql.SendAsync(
            other.Id, new UpdateServiceOfferingDetailsCommand(service, "Mine", 30), cancellationToken);
        var pricing = await mysql.SendAsync(
            other.Id,
            new ChangeServiceOfferingPricingCommand(service, new PricingData(PricingType.OnRequest)),
            cancellationToken);
        var deactivate = await mysql.SendAsync(other.Id, new DeactivateServiceOfferingCommand(service), cancellationToken);
        var list = await ListAsync(other.Id, new ListServiceOfferingsQuery(), cancellationToken);

        get.Error.Code.ShouldBe("ServiceOffering.NotFound");
        update.Error.Code.ShouldBe("ServiceOffering.NotFound");
        pricing.Error.Code.ShouldBe("ServiceOffering.NotFound");
        deactivate.Error.Code.ShouldBe("ServiceOffering.NotFound");
        list.Items.ShouldBeEmpty();
        list.TotalCount.ShouldBe(0);
        (await GetAsync(company.Id, service, cancellationToken)).IsActive.ShouldBeTrue();
    }

    private async Task<Guid> CreateCategoryAsync(Guid companyId, string name, CancellationToken cancellationToken) =>
        (await mysql.SendAsync(companyId, new CreateServiceCategoryCommand(name), cancellationToken)).Value;

    private async Task<Guid> CreateServiceAsync(
        Guid companyId,
        string name,
        CancellationToken cancellationToken,
        decimal? price = 50m,
        Guid? categoryId = null) =>
        (await mysql.SendAsync(
            companyId,
            new CreateServiceOfferingCommand(
                name,
                30,
                new PricingData(price is null ? PricingType.OnRequest : PricingType.Fixed, price),
                null,
                categoryId),
            cancellationToken)).Value;

    private async Task<ServiceOfferingDto> GetAsync(Guid companyId, Guid serviceId, CancellationToken cancellationToken) =>
        (await mysql.QueryAsync(companyId, new GetServiceOfferingQuery(serviceId), cancellationToken)).Value;

    private async Task<PagedResult<ServiceOfferingSummaryDto>> ListAsync(
        Guid companyId,
        ListServiceOfferingsQuery query,
        CancellationToken cancellationToken) =>
        (await mysql.QueryAsync(companyId, query, cancellationToken)).Value;

    private static string[] Names(PagedResult<ServiceOfferingSummaryDto> page) =>
        page.Items.Select(item => item.Name).ToArray();
}