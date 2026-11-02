using System.Globalization;
using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.ServiceOfferings;
using PlanelyApp.Application.Catalog.ServiceOfferings.Create;
using PlanelyApp.Domain.Catalog.ServiceCategories;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.SeedWork;
using Shouldly;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;

namespace PlanelyApp.Application.Tests.Catalog;

public class CreateServiceOfferingTests
{
    private readonly IServiceOfferingRepository _services = Substitute.For<IServiceOfferingRepository>();
    private readonly IServiceCategoryRepository _categories = Substitute.For<IServiceCategoryRepository>();
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public async Task A_fixed_price_service_should_be_added_to_the_company_of_the_request()
    {
        var added = CaptureAdded();

        var result = await Handler().HandleAsync(
            new CreateServiceOfferingCommand("  Haircut ", 45, new PricingData(PricingType.Fixed, 59.9m), " Scissors "),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var service = added()!;
        result.Value.ShouldBe(service.Id);
        service.CompanyId.ShouldBe(_companyId);
        service.Name.ShouldBe("Haircut");
        service.Description.ShouldBe("Scissors");
        service.StandardDuration.ShouldBe(TimeSpan.FromMinutes(45));
        service.PricingType.ShouldBe(PricingType.Fixed);
        service.BasePrice!.Amount.ShouldBe(59.9m);
        service.CategoryId.ShouldBeNull();
        service.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task An_on_request_service_should_be_added_without_a_price()
    {
        var added = CaptureAdded();

        var result = await Handler().HandleAsync(
            new CreateServiceOfferingCommand("Custom tattoo", 120, new PricingData(PricingType.OnRequest)),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        added()!.PricingType.ShouldBe(PricingType.OnRequest);
        added()!.BasePrice.ShouldBeNull();
    }

    [Fact]
    public async Task A_service_in_a_category_of_the_company_should_keep_the_category()
    {
        var category = CatalogData.NewCategory(_companyId);
        _categories.GetByIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(category);
        var added = CaptureAdded();

        var result = await Handler().HandleAsync(
            new CreateServiceOfferingCommand("Haircut", 30, new PricingData(PricingType.Fixed, 50m), null, category.Id),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        added()!.CategoryId.ShouldBe(category.Id);
    }

    [Fact]
    public async Task A_category_that_does_not_exist_should_be_not_found_and_add_nothing()
    {
        var result = await Handler().HandleAsync(
            new CreateServiceOfferingCommand("Haircut", 30, new PricingData(PricingType.Fixed, 50m), null, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("ServiceCategory.NotFound");
        _services.DidNotReceive().Add(Arg.Any<ServiceOffering>());
    }

    [Fact]
    public async Task A_name_already_in_the_company_should_be_a_conflict()
    {
        _services.FindByNameAsync(_companyId, "Haircut", Arg.Any<CancellationToken>())
            .Returns(CatalogData.NewFixedService(_companyId));

        var result = await Handler().HandleAsync(
            new CreateServiceOfferingCommand("Haircut", 30, new PricingData(PricingType.Fixed, 50m)),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("ServiceOffering.NameAlreadyExists");
        _services.DidNotReceive().Add(Arg.Any<ServiceOffering>());
    }

    [Fact]
    public async Task The_name_should_be_looked_up_trimmed_and_inside_the_company()
    {
        await Handler().HandleAsync(
            new CreateServiceOfferingCommand("  Haircut ", 30, new PricingData(PricingType.Fixed, 50m)),
            TestContext.Current.CancellationToken);

        await _services.Received(1).FindByNameAsync(_companyId, "Haircut", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_service_without_a_category_should_not_look_for_one()
    {
        await Handler().HandleAsync(
            new CreateServiceOfferingCommand("Haircut", 30, new PricingData(PricingType.Fixed, 50m)),
            TestContext.Current.CancellationToken);

        await _categories.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0, 50, "ServiceOffering.DurationInvalid")]
    [InlineData(1441, 50, "ServiceOffering.DurationInvalid")]
    [InlineData(30, -1, "Money.NegativeAmount")]
    public async Task Values_the_domain_refuses_should_be_refused_and_add_nothing(int minutes, int price, string code)
    {
        var exception = await Should.ThrowAsync<DomainException>(() => Handler().HandleAsync(
            new CreateServiceOfferingCommand("Haircut", minutes, new PricingData(PricingType.Fixed, price)),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(code);
        _services.DidNotReceive().Add(Arg.Any<ServiceOffering>());
    }

    [Fact]
    public async Task A_name_above_the_limit_should_be_refused_by_the_domain()
    {
        var exception = await Should.ThrowAsync<DomainException>(() => Handler().HandleAsync(
            new CreateServiceOfferingCommand(new string('x', 151), 30, new PricingData(PricingType.Fixed, 50m)),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("ServiceOffering.NameInvalid");
    }

    [Fact]
    public async Task A_request_without_a_company_should_fail_loudly()
    {
        var handler = new CreateServiceOfferingHandler(_services, _categories, Substitute.For<ITenantProvider>());

        await Should.ThrowAsync<InvalidOperationException>(() => handler.HandleAsync(
            new CreateServiceOfferingCommand("Haircut", 30, new PricingData(PricingType.Fixed, 50m)),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_valid_fixed_price_command_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new CreateServiceOfferingValidator(),
            new CreateServiceOfferingCommand("Haircut", 30, new PricingData(PricingType.Fixed, 50m), "Text", Guid.NewGuid()));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_valid_on_request_command_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new CreateServiceOfferingValidator(),
            new CreateServiceOfferingCommand("Custom tattoo", 120, new PricingData(PricingType.OnRequest)));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_fixed_price_without_a_price_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new CreateServiceOfferingValidator(),
            new CreateServiceOfferingCommand("Haircut", 30, new PricingData(PricingType.Fixed)));

        problems.ShouldBe(["Pricing.BasePrice:Validation.Required"], ignoreOrder: true);
    }

    [Fact]
    public async Task An_on_request_service_with_a_price_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new CreateServiceOfferingValidator(),
            new CreateServiceOfferingCommand("Custom tattoo", 120, new PricingData(PricingType.OnRequest, 10m)));

        problems.ShouldBe(["Pricing.BasePrice:Validation.Invalid"], ignoreOrder: true);
    }

    [Theory]
    [InlineData("10.50", true)]
    [InlineData("9999999999.99", true)]
    [InlineData("10.999", false)]
    [InlineData("10000000000", false)]
    public async Task Only_a_price_that_fits_the_column_should_pass_the_validator(string price, bool accepted)
    {
        var problems = await TestValidation.RunAsync(
            new CreateServiceOfferingValidator(),
            new CreateServiceOfferingCommand(
                "Haircut", 30, new PricingData(PricingType.Fixed, decimal.Parse(price, CultureInfo.InvariantCulture))));

        if (accepted)
        {
            problems.ShouldBeEmpty();
        }
        else
        {
            problems.ShouldBe(["Pricing.BasePrice:Validation.Invalid"], ignoreOrder: true);
        }
    }

    [Fact]
    public async Task Missing_values_should_be_reported()
    {
        var problems = await TestValidation.RunAsync(
            new CreateServiceOfferingValidator(),
            new CreateServiceOfferingCommand(" ", 30, null!, null, Guid.Empty));

        problems.ShouldBe(
            ["Name:Validation.Required", "Pricing:Validation.Required", "CategoryId:Validation.Invalid"],
            ignoreOrder: true);
    }

    [Fact]
    public async Task A_pricing_type_that_does_not_exist_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new CreateServiceOfferingValidator(),
            new CreateServiceOfferingCommand("Haircut", 30, new PricingData((PricingType)99)));

        problems.ShouldBe(["Pricing.Type:Validation.Invalid"], ignoreOrder: true);
    }

    private CreateServiceOfferingHandler Handler() => new(_services, _categories, BranchData.TenantOf(_companyId));

    private Func<ServiceOffering?> CaptureAdded()
    {
        ServiceOffering? added = null;
        _services.When(repository => repository.Add(Arg.Any<ServiceOffering>()))
            .Do(call => added = call.Arg<ServiceOffering>());

        return () => added;
    }
}