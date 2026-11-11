using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.ServiceOfferings.UpdateDetails;
using PlanelyApp.Domain.Catalog.ServiceCategories;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests.Catalog;

public class UpdateServiceOfferingDetailsTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly IServiceCategoryRepository _categories = Substitute.For<IServiceCategoryRepository>();

    [Fact]
    public async Task Updating_should_replace_the_details_and_keep_the_price()
    {
        var category = CatalogData.NewCategory(_companyId);
        _categories.GetByIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(category);
        var service = CatalogData.NewFixedService(_companyId);

        var result = await Handler(service).HandleAsync(
            new UpdateServiceOfferingDetailsCommand(service.Id, "  Beard trim ", 20, " Quick ", category.Id),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        service.Name.ShouldBe("Beard trim");
        service.Description.ShouldBe("Quick");
        service.StandardDuration.ShouldBe(TimeSpan.FromMinutes(20));
        service.CategoryId.ShouldBe(category.Id);
        service.BasePrice!.Amount.ShouldBe(50m);
    }

    [Fact]
    public async Task A_missing_service_should_be_not_found()
    {
        var result = await Handler(null).HandleAsync(
            new UpdateServiceOfferingDetailsCommand(Guid.NewGuid(), "Haircut", 30),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("ServiceOffering.NotFound");
    }

    [Fact]
    public async Task A_category_that_does_not_exist_should_be_not_found_and_leave_the_service_alone()
    {
        var service = CatalogData.NewFixedService(_companyId);

        var result = await Handler(service).HandleAsync(
            new UpdateServiceOfferingDetailsCommand(service.Id, "Beard trim", 20, null, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("ServiceCategory.NotFound");
        service.Name.ShouldBe("Haircut");
    }

    [Fact]
    public async Task Without_a_category_the_service_should_leave_its_category()
    {
        var service = CatalogData.NewFixedService(_companyId);
        service.UpdateDetails("Haircut", null, TimeSpan.FromMinutes(30), Guid.NewGuid());

        var result = await Handler(service).HandleAsync(
            new UpdateServiceOfferingDetailsCommand(service.Id, "Haircut", 30),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        service.CategoryId.ShouldBeNull();
        await _categories.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_name_used_by_another_service_should_be_a_conflict()
    {
        var service = CatalogData.NewFixedService(_companyId);
        var services = CatalogData.ServicesWith(service);
        services.FindByNameAsync(_companyId, "Shave", Arg.Any<CancellationToken>())
            .Returns(CatalogData.NewFixedService(_companyId, "Shave"));

        var result = await new UpdateServiceOfferingDetailsHandler(services, _categories).HandleAsync(
            new UpdateServiceOfferingDetailsCommand(service.Id, "Shave", 30), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("ServiceOffering.NameAlreadyExists");
        service.Name.ShouldBe("Haircut");
    }

    [Fact]
    public async Task The_service_found_by_its_own_name_should_not_be_a_conflict()
    {
        var service = CatalogData.NewFixedService(_companyId);
        var services = CatalogData.ServicesWith(service);
        services.FindByNameAsync(_companyId, "Haircut", Arg.Any<CancellationToken>()).Returns(service);

        var result = await new UpdateServiceOfferingDetailsHandler(services, _categories).HandleAsync(
            new UpdateServiceOfferingDetailsCommand(service.Id, "Haircut", 45), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        service.StandardDuration.ShouldBe(TimeSpan.FromMinutes(45));
    }

    [Fact]
    public async Task A_value_the_domain_refuses_should_leave_the_service_untouched()
    {
        var service = CatalogData.NewFixedService(_companyId);

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(service).HandleAsync(
            new UpdateServiceOfferingDetailsCommand(service.Id, "Beard trim", 0),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("ServiceOffering.DurationInvalid");
        service.Name.ShouldBe("Haircut");
        service.StandardDuration.ShouldBe(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task A_valid_command_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new UpdateServiceOfferingDetailsValidator(),
            new UpdateServiceOfferingDetailsCommand(Guid.NewGuid(), "Haircut", 30, "Text", Guid.NewGuid()));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task Missing_values_should_be_reported()
    {
        var problems = await TestValidation.RunAsync(
            new UpdateServiceOfferingDetailsValidator(),
            new UpdateServiceOfferingDetailsCommand(Guid.Empty, " ", 30, null, Guid.Empty));

        problems.ShouldBe(
            ["ServiceOfferingId:Validation.Required", "Name:Validation.Required", "CategoryId:Validation.Invalid"],
            ignoreOrder: true);
    }

    private UpdateServiceOfferingDetailsHandler Handler(PlanelyApp.Domain.Catalog.ServiceOfferings.ServiceOffering? service) =>
        new(CatalogData.ServicesWith(service), _categories);
}