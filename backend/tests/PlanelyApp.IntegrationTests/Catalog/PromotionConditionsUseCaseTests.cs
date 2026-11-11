using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.Promotions;
using PlanelyApp.Application.Catalog.Promotions.ChangeDiscount;
using PlanelyApp.Application.Catalog.Promotions.ChangeValidity;
using PlanelyApp.Application.Catalog.Promotions.Create;
using PlanelyApp.Application.Catalog.Promotions.End;
using PlanelyApp.Application.Catalog.Promotions.Get;
using PlanelyApp.Application.Catalog.Promotions.SetWeekdays;
using PlanelyApp.Application.Catalog.Promotions.UpdateDetails;
using PlanelyApp.Application.Catalog.ServiceOfferings;
using PlanelyApp.Application.Catalog.ServiceOfferings.Create;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Catalog;

/// <summary>The whole way, as a request has it: dispatcher, decorators, handler, repository and MySQL.</summary>
public class PromotionConditionsUseCaseTests(MySqlFixture mysql)
{
    private static readonly DateOnly Start = new(2027, 11, 20);
    private static readonly DateOnly End = new(2027, 11, 30);

    [Fact]
    public async Task The_details_should_be_saved_and_the_other_conditions_kept()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (company, promotion) = await SeedAsync(cancellationToken);

        var result = await mysql.SendAsync(
            company, new UpdatePromotionDetailsCommand(promotion, "  Cyber Monday ", " Online only "), cancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var dto = await GetAsync(company, promotion, cancellationToken);
        dto.Name.ShouldBe("Cyber Monday");
        dto.Description.ShouldBe("Online only");
        dto.Discount.ShouldBe(new DiscountData(DiscountType.Percentage, 10m));
        dto.ValidFrom.ShouldBe(Start);
        dto.Weekdays.ShouldBe([DayOfWeek.Saturday], ignoreOrder: false);

        await mysql.SendAsync(company, new UpdatePromotionDetailsCommand(promotion, "Cyber Monday"), cancellationToken);
        (await GetAsync(company, promotion, cancellationToken)).Description.ShouldBeNull();
    }

    [Fact]
    public async Task The_discount_should_be_saved_and_the_other_conditions_kept()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (company, promotion) = await SeedAsync(cancellationToken);

        var result = await mysql.SendAsync(
            company,
            new ChangePromotionDiscountCommand(promotion, new DiscountData(DiscountType.FixedAmount, 15.5m)),
            cancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var dto = await GetAsync(company, promotion, cancellationToken);
        dto.Discount.ShouldBe(new DiscountData(DiscountType.FixedAmount, 15.5m));
        dto.Name.ShouldBe("Black Friday");
        dto.ValidUntil.ShouldBe(End);
    }

    [Fact]
    public async Task The_validity_and_the_weekdays_should_be_saved_and_the_weekdays_can_go_back_to_every_day()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (company, promotion) = await SeedAsync(cancellationToken);

        var validity = await mysql.SendAsync(
            company,
            new ChangePromotionValidityCommand(promotion, new DateOnly(2027, 12, 1), new DateOnly(2027, 12, 31)),
            cancellationToken);
        var weekdays = await mysql.SendAsync(
            company,
            new SetPromotionWeekdaysCommand(promotion, [DayOfWeek.Friday, DayOfWeek.Sunday]),
            cancellationToken);

        validity.IsSuccess.ShouldBeTrue();
        weekdays.IsSuccess.ShouldBeTrue();
        var dto = await GetAsync(company, promotion, cancellationToken);
        dto.ValidFrom.ShouldBe(new DateOnly(2027, 12, 1));
        dto.ValidUntil.ShouldBe(new DateOnly(2027, 12, 31));
        dto.Weekdays.ShouldBe([DayOfWeek.Sunday, DayOfWeek.Friday], ignoreOrder: false);
        dto.Discount.ShouldBe(new DiscountData(DiscountType.Percentage, 10m));

        await mysql.SendAsync(company, new SetPromotionWeekdaysCommand(promotion, []), cancellationToken);
        (await GetAsync(company, promotion, cancellationToken)).Weekdays.ShouldBeEmpty();
    }

    [Fact]
    public async Task What_the_domain_refuses_should_leave_the_saved_promotion_as_it_was()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (company, promotion) = await SeedAsync(cancellationToken);

        var discount = await Should.ThrowAsync<DomainException>(() => mysql.SendAsync(
            company,
            new ChangePromotionDiscountCommand(promotion, new DiscountData(DiscountType.Percentage, 101m)),
            cancellationToken));
        var validity = await Should.ThrowAsync<DomainException>(() => mysql.SendAsync(
            company,
            new ChangePromotionValidityCommand(promotion, new DateOnly(2027, 12, 1), new DateOnly(2027, 11, 1)),
            cancellationToken));
        var weekdays = await Should.ThrowAsync<DomainException>(() => mysql.SendAsync(
            company, new SetPromotionWeekdaysCommand(promotion, [DayOfWeek.Monday, (DayOfWeek)9]), cancellationToken));
        var name = await Should.ThrowAsync<DomainException>(() => mysql.SendAsync(
            company, new UpdatePromotionDetailsCommand(promotion, new string('x', 101)), cancellationToken));

        discount.Code.ShouldBe("Discount.PercentageInvalid");
        validity.Code.ShouldBe("ValidityPeriod.EndBeforeStart");
        weekdays.Code.ShouldBe("Promotion.WeekdayInvalid");
        name.Code.ShouldBe("Promotion.NameInvalid");
        var dto = await GetAsync(company, promotion, cancellationToken);
        dto.Name.ShouldBe("Black Friday");
        dto.Discount.ShouldBe(new DiscountData(DiscountType.Percentage, 10m));
        dto.ValidFrom.ShouldBe(Start);
        dto.ValidUntil.ShouldBe(End);
        dto.Weekdays.ShouldBe([DayOfWeek.Saturday], ignoreOrder: false);
    }

    [Fact]
    public async Task A_promotion_of_another_company_should_be_not_found_to_every_change()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (company, promotion) = await SeedAsync(cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        var results = new[]
        {
            await mysql.SendAsync(other.Id, new UpdatePromotionDetailsCommand(promotion, "Hacked"), cancellationToken),
            await mysql.SendAsync(
                other.Id,
                new ChangePromotionDiscountCommand(promotion, new DiscountData(DiscountType.Percentage, 99m)),
                cancellationToken),
            await mysql.SendAsync(
                other.Id, new ChangePromotionValidityCommand(promotion, Start, End), cancellationToken),
            await mysql.SendAsync(other.Id, new SetPromotionWeekdaysCommand(promotion, []), cancellationToken),
        };

        foreach (var result in results)
        {
            result.Error.Type.ShouldBe(ErrorType.NotFound);
            result.Error.Code.ShouldBe("Promotion.NotFound");
        }

        var dto = await GetAsync(company, promotion, cancellationToken);
        dto.Name.ShouldBe("Black Friday");
        dto.Discount.ShouldBe(new DiscountData(DiscountType.Percentage, 10m));
        dto.Weekdays.ShouldBe([DayOfWeek.Saturday], ignoreOrder: false);
    }

    [Fact]
    public async Task Changing_an_ended_promotion_should_not_bring_it_back()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (company, promotion) = await SeedAsync(cancellationToken);
        await mysql.SendAsync(company, new EndPromotionCommand(promotion), cancellationToken);

        var result = await mysql.SendAsync(
            company, new UpdatePromotionDetailsCommand(promotion, "Renamed"), cancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var dto = await GetAsync(company, promotion, cancellationToken);
        dto.Name.ShouldBe("Renamed");
        dto.IsActive.ShouldBeFalse();
    }

    // A per-service promotion for all branches, 10%, on Saturdays, from November 20 to 30.
    private async Task<(Guid CompanyId, Guid PromotionId)> SeedAsync(CancellationToken cancellationToken)
    {
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var haircut = (await mysql.SendAsync(
            company.Id,
            new CreateServiceOfferingCommand("Haircut", 30, new PricingData(PricingType.Fixed, 50m)),
            cancellationToken)).Value;

        var promotion = (await mysql.SendAsync(
            company.Id,
            new CreatePromotionCommand(
                PromotionKind.PerService,
                "Black Friday",
                new DiscountData(DiscountType.Percentage, 10m),
                Start,
                End,
                [haircut],
                true,
                Weekdays: [DayOfWeek.Saturday]),
            cancellationToken)).Value;

        return (company.Id, promotion);
    }

    private async Task<PromotionDto> GetAsync(Guid companyId, Guid promotionId, CancellationToken cancellationToken) =>
        (await mysql.QueryAsync(companyId, new GetPromotionQuery(promotionId), cancellationToken)).Value;
}