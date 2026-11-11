using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.Promotions;
using PlanelyApp.Application.Catalog.Promotions.ChangeBranches;
using PlanelyApp.Application.Catalog.Promotions.Create;
using PlanelyApp.Application.Catalog.Promotions.Get;
using PlanelyApp.Application.Catalog.Promotions.IncludeServices;
using PlanelyApp.Application.Catalog.Promotions.RemoveService;
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

/// <summary>The whole way, as a request has it: dispatcher, decorators, handler, repository and MySQL.</summary>
public class PromotionScopeUseCaseTests(MySqlFixture mysql)
{
    private static readonly DateOnly Start = new(2027, 11, 20);
    private static readonly DateOnly End = new(2027, 11, 30);

    [Fact]
    public async Task The_branches_should_be_replaced_and_can_go_back_to_all()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var haircut = await CreateServiceAsync(company.Id, "Haircut", 50m, cancellationToken);
        var centro = await CreateBranchAsync(company.Id, "Centro", cancellationToken);
        var norte = await CreateBranchAsync(company.Id, "Norte", cancellationToken);
        var promotion = await CreatePromotionAsync(company.Id, PromotionKind.PerService, [haircut], cancellationToken);

        var some = await mysql.SendAsync(
            company.Id, new ChangePromotionBranchesCommand(promotion, false, [norte, centro]), cancellationToken);

        some.IsSuccess.ShouldBeTrue();
        var dto = await GetAsync(company.Id, promotion, cancellationToken);
        dto.AppliesToAllBranches.ShouldBeFalse();
        dto.BranchIds.ShouldBe([norte, centro], ignoreOrder: false);

        var all = await mysql.SendAsync(company.Id, new ChangePromotionBranchesCommand(promotion, true), cancellationToken);

        all.IsSuccess.ShouldBeTrue();
        dto = await GetAsync(company.Id, promotion, cancellationToken);
        dto.AppliesToAllBranches.ShouldBeTrue();
        dto.BranchIds.ShouldBeEmpty();
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
        var promotion = await CreatePromotionAsync(company.Id, PromotionKind.PerService, [haircut], cancellationToken);

        var unknown = await mysql.SendAsync(
            company.Id, new ChangePromotionBranchesCommand(promotion, false, [mine, Guid.CreateVersion7()]), cancellationToken);
        var another = await mysql.SendAsync(
            company.Id, new ChangePromotionBranchesCommand(promotion, false, [mine, theirs]), cancellationToken);

        unknown.Error.Code.ShouldBe("Branch.NotFound");
        another.Error.Type.ShouldBe(ErrorType.NotFound);
        another.Error.Code.ShouldBe("Branch.NotFound");
        (await GetAsync(company.Id, promotion, cancellationToken)).AppliesToAllBranches.ShouldBeTrue();
    }

    [Fact]
    public async Task Included_services_should_be_added_at_the_end_and_repeats_ignored()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var haircut = await CreateServiceAsync(company.Id, "Haircut", 50m, cancellationToken);
        var beard = await CreateServiceAsync(company.Id, "Beard", 30m, cancellationToken);
        var wash = await CreateServiceAsync(company.Id, "Wash", 10m, cancellationToken);
        var promotion = await CreatePromotionAsync(company.Id, PromotionKind.PerService, [haircut], cancellationToken);

        var result = await mysql.SendAsync(
            company.Id, new IncludePromotionServicesCommand(promotion, [beard, haircut, wash]), cancellationToken);
        var again = await mysql.SendAsync(
            company.Id, new IncludePromotionServicesCommand(promotion, [beard]), cancellationToken);

        result.IsSuccess.ShouldBeTrue();
        again.IsSuccess.ShouldBeTrue();
        (await GetAsync(company.Id, promotion, cancellationToken)).ServiceIds
            .ShouldBe([haircut, beard, wash], ignoreOrder: false);
    }

    [Fact]
    public async Task A_service_of_another_company_or_on_request_should_not_be_included()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var haircut = await CreateServiceAsync(company.Id, "Haircut", 50m, cancellationToken);
        var beard = await CreateServiceAsync(company.Id, "Beard", 30m, cancellationToken);
        var theirs = await CreateServiceAsync(other.Id, "Haircut", 50m, cancellationToken);
        var tattoo = await CreateServiceAsync(company.Id, "Tattoo", null, cancellationToken);
        var promotion = await CreatePromotionAsync(company.Id, PromotionKind.PerService, [haircut], cancellationToken);

        var another = await mysql.SendAsync(
            company.Id, new IncludePromotionServicesCommand(promotion, [beard, theirs]), cancellationToken);
        var onRequest = await Should.ThrowAsync<DomainException>(() => mysql.SendAsync(
            company.Id, new IncludePromotionServicesCommand(promotion, [beard, tattoo]), cancellationToken));

        another.Error.Type.ShouldBe(ErrorType.NotFound);
        another.Error.Code.ShouldBe("ServiceOffering.NotFound");
        onRequest.Code.ShouldBe("Promotion.OnRequestServiceNotAllowed");
        (await GetAsync(company.Id, promotion, cancellationToken)).ServiceIds.ShouldBe([haircut], ignoreOrder: false);
    }

    [Fact]
    public async Task A_removed_service_should_be_saved_and_one_the_promotion_lacks_should_be_ignored()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var haircut = await CreateServiceAsync(company.Id, "Haircut", 50m, cancellationToken);
        var beard = await CreateServiceAsync(company.Id, "Beard", 30m, cancellationToken);
        var promotion = await CreatePromotionAsync(
            company.Id, PromotionKind.PerService, [haircut, beard], cancellationToken);

        var removed = await mysql.SendAsync(
            company.Id, new RemovePromotionServiceCommand(promotion, haircut), cancellationToken);
        var again = await mysql.SendAsync(
            company.Id, new RemovePromotionServiceCommand(promotion, haircut), cancellationToken);

        removed.IsSuccess.ShouldBeTrue();
        again.IsSuccess.ShouldBeTrue();
        (await GetAsync(company.Id, promotion, cancellationToken)).ServiceIds.ShouldBe([beard], ignoreOrder: false);
    }

    [Fact]
    public async Task The_last_service_and_the_second_of_a_bundle_should_not_be_removed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var haircut = await CreateServiceAsync(company.Id, "Haircut", 50m, cancellationToken);
        var beard = await CreateServiceAsync(company.Id, "Beard", 30m, cancellationToken);
        var single = await CreatePromotionAsync(company.Id, PromotionKind.PerService, [haircut], cancellationToken);
        var bundle = await CreatePromotionAsync(company.Id, PromotionKind.Bundle, [haircut, beard], cancellationToken);

        var last = await Should.ThrowAsync<DomainException>(() => mysql.SendAsync(
            company.Id, new RemovePromotionServiceCommand(single, haircut), cancellationToken));
        var below = await Should.ThrowAsync<DomainException>(() => mysql.SendAsync(
            company.Id, new RemovePromotionServiceCommand(bundle, beard), cancellationToken));

        last.Code.ShouldBe("Promotion.ServicesRequired");
        below.Code.ShouldBe("Promotion.BundleRequiresAtLeastTwoServices");
        (await GetAsync(company.Id, single, cancellationToken)).ServiceIds.ShouldBe([haircut], ignoreOrder: false);
        (await GetAsync(company.Id, bundle, cancellationToken)).ServiceIds.ShouldBe([haircut, beard], ignoreOrder: false);
    }

    [Fact]
    public async Task A_promotion_of_another_company_should_be_not_found_to_every_change_of_scope()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var haircut = await CreateServiceAsync(company.Id, "Haircut", 50m, cancellationToken);
        var beard = await CreateServiceAsync(company.Id, "Beard", 30m, cancellationToken);
        var promotion = await CreatePromotionAsync(
            company.Id, PromotionKind.PerService, [haircut, beard], cancellationToken);

        var results = new[]
        {
            await mysql.SendAsync(other.Id, new ChangePromotionBranchesCommand(promotion, true), cancellationToken),
            await mysql.SendAsync(
                other.Id, new IncludePromotionServicesCommand(promotion, [haircut]), cancellationToken),
            await mysql.SendAsync(other.Id, new RemovePromotionServiceCommand(promotion, haircut), cancellationToken),
        };

        foreach (var result in results)
        {
            result.Error.Type.ShouldBe(ErrorType.NotFound);
            result.Error.Code.ShouldBe("Promotion.NotFound");
        }

        (await GetAsync(company.Id, promotion, cancellationToken)).ServiceIds
            .ShouldBe([haircut, beard], ignoreOrder: false);
    }

    private async Task<Guid> CreatePromotionAsync(
        Guid companyId,
        PromotionKind kind,
        IReadOnlyList<Guid> serviceIds,
        CancellationToken cancellationToken) =>
        (await mysql.SendAsync(
            companyId,
            new CreatePromotionCommand(
                kind, "Black Friday", new DiscountData(DiscountType.Percentage, 10m), Start, End, serviceIds, true),
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
}