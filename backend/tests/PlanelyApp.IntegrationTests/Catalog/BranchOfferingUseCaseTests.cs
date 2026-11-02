using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.BranchOfferings;
using PlanelyApp.Application.Catalog.BranchOfferings.ClearPrice;
using PlanelyApp.Application.Catalog.BranchOfferings.List;
using PlanelyApp.Application.Catalog.BranchOfferings.SetAvailability;
using PlanelyApp.Application.Catalog.BranchOfferings.SetPrice;
using PlanelyApp.Application.Catalog.ServiceCategories.Create;
using PlanelyApp.Application.Catalog.ServiceOfferings;
using PlanelyApp.Application.Catalog.ServiceOfferings.ChangePricing;
using PlanelyApp.Application.Catalog.ServiceOfferings.Create;
using PlanelyApp.Application.Catalog.ServiceOfferings.Deactivate;
using PlanelyApp.Application.Organization.Branches.Create;
using PlanelyApp.Application.Organization.Shared;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Catalog;

/// <summary>The whole way, as a request has it: dispatcher, decorators, handler, repository or read model, and MySQL.</summary>
public class BranchOfferingUseCaseTests(MySqlFixture mysql)
{
    [Fact]
    public async Task A_price_and_an_unavailability_should_be_saved_and_listed_only_for_that_branch()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var centro = await CreateBranchAsync(company.Id, "Centro", cancellationToken);
        var norte = await CreateBranchAsync(company.Id, "Norte", cancellationToken);
        var haircut = await CreateServiceAsync(company.Id, "Haircut", 50m, cancellationToken);
        var shave = await CreateServiceAsync(company.Id, "Shave", 30m, cancellationToken);
        await CreateServiceAsync(company.Id, "Tattoo", null, cancellationToken);
        await CreateServiceAsync(company.Id, "Wash", 10m, cancellationToken);

        var priced = await mysql.SendAsync(
            company.Id, new SetBranchOfferingPriceCommand(centro, haircut, 40m), cancellationToken);
        var unavailable = await mysql.SendAsync(
            company.Id, new SetBranchOfferingAvailabilityCommand(centro, shave, false), cancellationToken);
        var inCentro = await ListAsync(company.Id, new ListBranchOfferingsQuery(centro), cancellationToken);
        var inNorte = await ListAsync(company.Id, new ListBranchOfferingsQuery(norte), cancellationToken);

        priced.IsSuccess.ShouldBeTrue();
        unavailable.IsSuccess.ShouldBeTrue();
        Names(inCentro).ShouldBe(["Haircut", "Shave", "Tattoo", "Wash"], ignoreOrder: false);
        var centroHaircut = Item(inCentro, "Haircut");
        centroHaircut.BasePrice.ShouldBe(50m);
        centroHaircut.CustomPrice.ShouldBe(40m);
        centroHaircut.Price.ShouldBe(40m);
        centroHaircut.IsAvailable.ShouldBeTrue();
        var centroShave = Item(inCentro, "Shave");
        centroShave.CustomPrice.ShouldBeNull();
        centroShave.Price.ShouldBe(30m);
        centroShave.IsAvailable.ShouldBeFalse();
        var centroTattoo = Item(inCentro, "Tattoo");
        centroTattoo.PricingType.ShouldBe(PricingType.OnRequest);
        centroTattoo.BasePrice.ShouldBeNull();
        centroTattoo.Price.ShouldBeNull();
        Item(inCentro, "Wash").Price.ShouldBe(10m);
        Item(inNorte, "Haircut").CustomPrice.ShouldBeNull();
        Item(inNorte, "Haircut").Price.ShouldBe(50m);
        Item(inNorte, "Shave").IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task Clearing_the_price_and_offering_again_should_go_back_to_the_defaults()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var centro = await CreateBranchAsync(company.Id, "Centro", cancellationToken);
        var haircut = await CreateServiceAsync(company.Id, "Haircut", 50m, cancellationToken);
        await mysql.SendAsync(company.Id, new SetBranchOfferingPriceCommand(centro, haircut, 40m), cancellationToken);
        await mysql.SendAsync(
            company.Id, new SetBranchOfferingAvailabilityCommand(centro, haircut, false), cancellationToken);

        var cleared = await mysql.SendAsync(
            company.Id, new ClearBranchOfferingPriceCommand(centro, haircut), cancellationToken);
        var clearedAgain = await mysql.SendAsync(
            company.Id, new ClearBranchOfferingPriceCommand(centro, haircut), cancellationToken);
        var offered = await mysql.SendAsync(
            company.Id, new SetBranchOfferingAvailabilityCommand(centro, haircut, true), cancellationToken);
        var item = Item(await ListAsync(company.Id, new ListBranchOfferingsQuery(centro), cancellationToken), "Haircut");

        cleared.IsSuccess.ShouldBeTrue();
        clearedAgain.IsSuccess.ShouldBeTrue();
        offered.IsSuccess.ShouldBeTrue();
        item.CustomPrice.ShouldBeNull();
        item.Price.ShouldBe(50m);
        item.IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task A_service_on_request_should_refuse_a_price_and_ignore_an_old_one()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var centro = await CreateBranchAsync(company.Id, "Centro", cancellationToken);
        var tattoo = await CreateServiceAsync(company.Id, "Tattoo", null, cancellationToken);
        var haircut = await CreateServiceAsync(company.Id, "Haircut", 50m, cancellationToken);

        var refused = await Should.ThrowAsync<DomainException>(() => mysql.SendAsync(
            company.Id, new SetBranchOfferingPriceCommand(centro, tattoo, 40m), cancellationToken));
        await mysql.SendAsync(company.Id, new SetBranchOfferingPriceCommand(centro, haircut, 40m), cancellationToken);
        await mysql.SendAsync(
            company.Id,
            new ChangeServiceOfferingPricingCommand(haircut, new PricingData(PricingType.OnRequest)),
            cancellationToken);
        var onRequest = Item(await ListAsync(company.Id, new ListBranchOfferingsQuery(centro), cancellationToken), "Haircut");
        await mysql.SendAsync(
            company.Id,
            new ChangeServiceOfferingPricingCommand(haircut, new PricingData(PricingType.Fixed, 80m)),
            cancellationToken);
        var fixedAgain = Item(await ListAsync(company.Id, new ListBranchOfferingsQuery(centro), cancellationToken), "Haircut");

        refused.Code.ShouldBe("Branch.CustomPriceNotAllowed");
        onRequest.PricingType.ShouldBe(PricingType.OnRequest);
        onRequest.BasePrice.ShouldBeNull();
        onRequest.CustomPrice.ShouldBeNull();
        onRequest.Price.ShouldBeNull();
        fixedAgain.BasePrice.ShouldBe(80m);
        fixedAgain.CustomPrice.ShouldBe(40m);
        fixedAgain.Price.ShouldBe(40m);
    }

    [Fact]
    public async Task The_list_should_skip_inactive_services_and_filter_search_and_page()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var centro = await CreateBranchAsync(company.Id, "Centro", cancellationToken);
        var hair = (await mysql.SendAsync(company.Id, new CreateServiceCategoryCommand("Hair"), cancellationToken)).Value;
        var beard = (await mysql.SendAsync(company.Id, new CreateServiceCategoryCommand("Beard"), cancellationToken)).Value;
        await CreateServiceAsync(company.Id, "Alpha cut", 50m, cancellationToken, hair);
        await CreateServiceAsync(company.Id, "Beta shave", 30m, cancellationToken, beard);
        await CreateServiceAsync(company.Id, "Gamma color", 90m, cancellationToken, hair);
        var delta = await CreateServiceAsync(company.Id, "Delta wash", 10m, cancellationToken);
        var epsilon = await CreateServiceAsync(company.Id, "Epsilon spa", 70m, cancellationToken);
        await mysql.SendAsync(company.Id, new DeactivateServiceOfferingCommand(delta), cancellationToken);
        await mysql.SendAsync(
            company.Id, new SetBranchOfferingAvailabilityCommand(centro, epsilon, false), cancellationToken);

        var all = await ListAsync(company.Id, new ListBranchOfferingsQuery(centro), cancellationToken);
        var unavailable = await ListAsync(
            company.Id, new ListBranchOfferingsQuery(centro, IsAvailable: false), cancellationToken);
        var available = await ListAsync(
            company.Id, new ListBranchOfferingsQuery(centro, IsAvailable: true), cancellationToken);
        var inHair = await ListAsync(company.Id, new ListBranchOfferingsQuery(centro, CategoryId: hair), cancellationToken);
        var searched = await ListAsync(company.Id, new ListBranchOfferingsQuery(centro, "SHAVE"), cancellationToken);
        var secondPage = await ListAsync(
            company.Id, new ListBranchOfferingsQuery(centro, Page: 2, PageSize: 2), cancellationToken);

        Names(all).ShouldBe(["Alpha cut", "Beta shave", "Epsilon spa", "Gamma color"], ignoreOrder: false);
        all.TotalCount.ShouldBe(4);
        Item(all, "Alpha cut").CategoryName.ShouldBe("Hair");
        Item(all, "Beta shave").CategoryName.ShouldBe("Beard");
        Item(all, "Epsilon spa").CategoryId.ShouldBeNull();
        Names(unavailable).ShouldBe(["Epsilon spa"], ignoreOrder: false);
        Names(available).ShouldBe(["Alpha cut", "Beta shave", "Gamma color"], ignoreOrder: false);
        Names(inHair).ShouldBe(["Alpha cut", "Gamma color"], ignoreOrder: false);
        Names(searched).ShouldBe(["Beta shave"], ignoreOrder: false);
        Names(secondPage).ShouldBe(["Epsilon spa", "Gamma color"], ignoreOrder: false);
        secondPage.TotalPages.ShouldBe(2);
    }

    [Fact]
    public async Task A_branch_or_a_service_of_another_company_should_be_not_found()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var centro = await CreateBranchAsync(company.Id, "Centro", cancellationToken);
        var haircut = await CreateServiceAsync(company.Id, "Haircut", 50m, cancellationToken);
        var foreignBranch = await CreateBranchAsync(other.Id, "Centro", cancellationToken);
        var foreignService = await CreateServiceAsync(other.Id, "Haircut", 50m, cancellationToken);

        var unknownList = await mysql.QueryAsync(
            company.Id, new ListBranchOfferingsQuery(Guid.NewGuid()), cancellationToken);
        var foreignList = await mysql.QueryAsync(other.Id, new ListBranchOfferingsQuery(centro), cancellationToken);
        var foreignBranchPrice = await mysql.SendAsync(
            company.Id, new SetBranchOfferingPriceCommand(foreignBranch, haircut, 40m), cancellationToken);
        var foreignServicePrice = await mysql.SendAsync(
            company.Id, new SetBranchOfferingPriceCommand(centro, foreignService, 40m), cancellationToken);
        var foreignServiceClear = await mysql.SendAsync(
            company.Id, new ClearBranchOfferingPriceCommand(centro, foreignService), cancellationToken);
        var foreignServiceAvailability = await mysql.SendAsync(
            company.Id, new SetBranchOfferingAvailabilityCommand(centro, foreignService, false), cancellationToken);

        unknownList.Error.Type.ShouldBe(ErrorType.NotFound);
        unknownList.Error.Code.ShouldBe("Branch.NotFound");
        foreignList.Error.Code.ShouldBe("Branch.NotFound");
        foreignBranchPrice.Error.Code.ShouldBe("Branch.NotFound");
        foreignServicePrice.Error.Code.ShouldBe("ServiceOffering.NotFound");
        foreignServiceClear.Error.Code.ShouldBe("ServiceOffering.NotFound");
        foreignServiceAvailability.Error.Code.ShouldBe("ServiceOffering.NotFound");
    }

    private async Task<Guid> CreateBranchAsync(Guid companyId, string name, CancellationToken cancellationToken) =>
        (await mysql.SendAsync(
            companyId,
            new CreateBranchCommand(
                name,
                new AddressData("Rua das Flores", "123", "Sala 4", "Centro", "São Paulo", "SP", "01310100"),
                "+551133334444",
                "America/Sao_Paulo"),
            cancellationToken)).Value;

    private async Task<Guid> CreateServiceAsync(
        Guid companyId,
        string name,
        decimal? price,
        CancellationToken cancellationToken,
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

    private async Task<PagedResult<BranchOfferingDto>> ListAsync(
        Guid companyId,
        ListBranchOfferingsQuery query,
        CancellationToken cancellationToken) =>
        (await mysql.QueryAsync(companyId, query, cancellationToken)).Value;

    private static BranchOfferingDto Item(PagedResult<BranchOfferingDto> page, string name) =>
        page.Items.Single(item => item.Name == name);

    private static string[] Names(PagedResult<BranchOfferingDto> page) =>
        page.Items.Select(item => item.Name).ToArray();
}