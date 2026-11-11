using System.Text.Json;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.ServiceOfferings;
using PlanelyApp.Application.Catalog.Promotions;
using PlanelyApp.Application.Catalog.Promotions.Create;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.IntegrationTests.Messaging;
using PlanelyApp.IntegrationTests.Organization;
using PlanelyApp.IntegrationTests.Scheduling;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Catalog;

/// <summary>What creating a promotion leaves in the outbox.</summary>
public class PromotionMessagesUseCaseTests(MySqlFixture mysql)
{
    private static readonly DateOnly Start = new(2027, 11, 20);
    private static readonly DateOnly End = new(2027, 11, 30);

    [Fact]
    public async Task A_promotion_for_some_branches_should_leave_a_message_with_them()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var haircut = await ServiceAsync(company.Id, "Haircut", cancellationToken);
        var centro = await BookingData.CreateBranchAsync(mysql, company.Id, "Centro", cancellationToken);

        var promotion = (await mysql.SendAsync(
            company.Id, NewPromotion([haircut], false, [centro]), cancellationToken)).Value;

        var message = (await OutboxData.ForCompanyAsync(mysql, company.Id, cancellationToken)).Single();
        message.Type.ShouldBe("promotion.created");
        message.Version.ShouldBe(1);
        using var data = JsonDocument.Parse(message.Payload);
        var root = data.RootElement;
        root.EnumerateObject().Select(property => property.Name).ToList()
            .ShouldBe(["promotionId", "appliesToAllBranches", "branchIds"], ignoreOrder: true);
        root.GetProperty("promotionId").GetGuid().ShouldBe(promotion);
        root.GetProperty("appliesToAllBranches").GetBoolean().ShouldBeFalse();
        root.GetProperty("branchIds").EnumerateArray().Select(branch => branch.GetGuid()).ToList()
            .ShouldBe([centro], ignoreOrder: false);
    }

    [Fact]
    public async Task A_promotion_for_all_branches_should_leave_a_message_with_an_empty_list()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var haircut = await ServiceAsync(company.Id, "Haircut", cancellationToken);

        await mysql.SendAsync(company.Id, NewPromotion([haircut], true), cancellationToken);

        var message = (await OutboxData.ForCompanyAsync(mysql, company.Id, cancellationToken)).Single();
        using var data = JsonDocument.Parse(message.Payload);
        data.RootElement.GetProperty("appliesToAllBranches").GetBoolean().ShouldBeTrue();
        data.RootElement.GetProperty("branchIds").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task A_refused_promotion_should_leave_no_message()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var theirs = await ServiceAsync(other.Id, "Haircut", cancellationToken);

        var result = await mysql.SendAsync(company.Id, NewPromotion([theirs], true), cancellationToken);

        result.Error.Code.ShouldBe("ServiceOffering.NotFound");
        (await OutboxData.ForCompanyAsync(mysql, company.Id, cancellationToken)).ShouldBeEmpty();
    }

    private Task<Guid> ServiceAsync(Guid companyId, string name, CancellationToken cancellationToken) =>
        BookingData.CreateServiceAsync(mysql, companyId, name, new PricingData(PricingType.Fixed, 50m), cancellationToken);

    private static CreatePromotionCommand NewPromotion(
        IReadOnlyList<Guid> services, bool allBranches, IReadOnlyList<Guid>? branches = null) =>
        new(
            PromotionKind.PerService,
            "Black Friday",
            new DiscountData(DiscountType.Percentage, 10m),
            Start,
            End,
            services,
            allBranches,
            branches);
}