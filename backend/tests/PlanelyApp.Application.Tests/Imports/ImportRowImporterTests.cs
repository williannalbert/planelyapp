using NSubstitute;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Imports;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.ServiceCategories.Create;
using PlanelyApp.Application.Catalog.ServiceOfferings;
using PlanelyApp.Application.Catalog.ServiceOfferings.Create;
using PlanelyApp.Application.Imports.Processing;
using PlanelyApp.Application.Organization.Customers.Register;
using PlanelyApp.Domain.Catalog.ServiceCategories;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Imports;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests.Imports;

public class ImportRowImporterTests
{
    private static readonly Guid CategoryId = Guid.CreateVersion7();
    private static readonly Guid CreatedId = Guid.CreateVersion7();
    private static readonly string[] ServiceHeaders = ["Name", "Duration", "Price", "Description", "Category"];
    private static readonly string[] CustomerHeaders = ["Name", "Email", "Phone", "Marketing", "Language"];

    private readonly IDispatcher _dispatcher = Substitute.For<IDispatcher>();
    private readonly IServiceCategoryRepository _categories = Substitute.For<IServiceCategoryRepository>();
    private readonly List<object> _sent = [];
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public ImportRowImporterTests() =>
        Replies(command => command is CreateServiceCategoryCommand ? CategoryId : CreatedId);

    [Fact]
    public async Task A_service_with_a_new_category_should_create_the_category_and_then_the_service()
    {
        var result = await ImportAsync(
            ImportType.Services, ServiceHeaders, " Haircut ", "30", "50,5", "Classic cut", "Hair");

        result.Success.ShouldBeTrue();
        result.Reason.ShouldBeNull();
        _sent.Count.ShouldBe(2);
        _sent[0].ShouldBe(new CreateServiceCategoryCommand("Hair"));
        _sent[1].ShouldBe(new CreateServiceOfferingCommand(
            "Haircut", 30, new PricingData(PricingType.Fixed, 50.5m), "Classic cut", CategoryId));
    }

    [Fact]
    public async Task A_category_that_exists_should_be_reused()
    {
        var existing = ServiceCategory.Create(Guid.CreateVersion7(), "Hair");
        _categories.FindByNameAsync("Hair", Arg.Any<CancellationToken>()).Returns(existing);

        var result = await ImportAsync(ImportType.Services, ServiceHeaders, "Haircut", "30", "", "", "Hair");

        result.Success.ShouldBeTrue();
        _sent.Single().ShouldBeOfType<CreateServiceOfferingCommand>().CategoryId.ShouldBe(existing.Id);
    }

    [Fact]
    public async Task A_service_with_no_price_should_be_on_request_and_ask_for_no_category()
    {
        var result = await ImportAsync(ImportType.Services, ServiceHeaders, "Haircut", "30", "", "", "");

        result.Success.ShouldBeTrue();
        _sent.Single().ShouldBe(new CreateServiceOfferingCommand("Haircut", 30, new PricingData(PricingType.OnRequest)));
        await _categories.DidNotReceiveWithAnyArgs().FindByNameAsync(default!, _ct);
    }

    [Theory]
    [InlineData("", "30", "", "Import.ValueRequired(name)")]
    [InlineData("Haircut", "", "", "Import.ValueRequired(duration)")]
    [InlineData("Haircut", "abc", "", "Import.ValueInvalid(duration)")]
    [InlineData("Haircut", "30", "abc", "Import.ValueInvalid(price)")]
    public async Task A_bad_cell_should_refuse_the_row_before_anything_is_created(
        string name, string duration, string price, string expectedReason)
    {
        // The category is filled on purpose: a bad row must not leave it behind.
        var result = await ImportAsync(ImportType.Services, ServiceHeaders, name, duration, price, "", "Hair");

        result.Success.ShouldBeFalse();
        result.Reason.ShouldBe(expectedReason);
        _sent.ShouldBeEmpty();
        await _categories.DidNotReceiveWithAnyArgs().FindByNameAsync(default!, _ct);
    }

    [Fact]
    public async Task A_conflict_of_the_use_case_should_be_the_reason_of_the_row()
    {
        Replies(_ => Result<Guid>.Failure(Error.Conflict("ServiceOffering.NameAlreadyExists")));

        var result = await ImportAsync(ImportType.Services, ServiceHeaders, "Haircut", "30", "", "", "");

        result.Success.ShouldBeFalse();
        result.Reason.ShouldBe("ServiceOffering.NameAlreadyExists");
    }

    [Fact]
    public async Task A_validation_error_should_list_its_fields()
    {
        Replies(_ => Result<Guid>.Failure(new ValidationError(
            [new FieldError("Name", "Validation.TooLong"), new FieldError("Email", "Validation.Invalid")])));

        var result = await ImportAsync(ImportType.Customers, CustomerHeaders, "Ana", "x", "", "", "");

        result.Reason.ShouldBe("Name:Validation.TooLong; Email:Validation.Invalid");
    }

    [Fact]
    public async Task A_rule_of_the_domain_should_be_the_reason_with_its_arguments()
    {
        Replies(_ => throw new DomainException("ServiceOffering.DurationInvalid", 5, "x"));

        var result = await ImportAsync(ImportType.Services, ServiceHeaders, "Haircut", "5", "", "", "");

        result.Success.ShouldBeFalse();
        result.Reason.ShouldBe("ServiceOffering.DurationInvalid(5;x)");
    }

    [Fact]
    public async Task A_category_that_cannot_be_created_should_refuse_the_row_without_creating_the_service()
    {
        Replies(command => command is CreateServiceCategoryCommand
            ? Result<Guid>.Failure(Error.Conflict("ServiceCategory.NameAlreadyExists"))
            : CreatedId);

        var result = await ImportAsync(ImportType.Services, ServiceHeaders, "Haircut", "30", "", "", "Hair");

        result.Reason.ShouldBe("ServiceCategory.NameAlreadyExists");
        _sent.Count.ShouldBe(1);
        _sent[0].ShouldBeOfType<CreateServiceCategoryCommand>();
    }

    [Fact]
    public async Task A_customer_should_be_registered_with_the_cells_read()
    {
        var result = await ImportAsync(
            ImportType.Customers, CustomerHeaders, "  Ana ", "ana@example.com", "5511999990001", "sim", "pt-BR");

        result.Success.ShouldBeTrue();
        _sent.Single().ShouldBe(new RegisterCustomerCommand("Ana", "ana@example.com", "+5511999990001", true, "pt-BR"));
    }

    [Fact]
    public async Task Blank_cells_of_a_customer_should_be_not_informed()
    {
        var result = await ImportAsync(ImportType.Customers, CustomerHeaders, "", "ana@example.com", "", "", "");

        result.Success.ShouldBeTrue();
        _sent.Single().ShouldBe(new RegisterCustomerCommand(null, "ana@example.com", null, false, null));
    }

    [Fact]
    public async Task A_marketing_cell_that_is_neither_yes_nor_no_should_refuse_the_row()
    {
        var result = await ImportAsync(ImportType.Customers, CustomerHeaders, "Ana", "ana@example.com", "", "talvez", "");

        result.Reason.ShouldBe("Import.ValueInvalid(marketing)");
        _sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_customer_that_is_already_registered_should_be_the_reason_of_the_row()
    {
        Replies(_ => Result<Guid>.Failure(Error.Conflict("Customer.EmailAlreadyRegistered")));

        var result = await ImportAsync(ImportType.Customers, CustomerHeaders, "Ana", "ana@example.com", "", "", "");

        result.Reason.ShouldBe("Customer.EmailAlreadyRegistered");
    }

    private Task<ImportRowResult> ImportAsync(ImportType type, string[] headers, params string[] cells) =>
        new ImportRowImporter(_dispatcher, _categories).ImportAsync(
            new ImportWork(type, 0, ImportSchemas.MapHeaders(headers), []),
            new ImportSheetRow(2, cells),
            _ct);

    private void Replies(Func<ICommand<Guid>, Result<Guid>> reply) =>
        _dispatcher.SendAsync(Arg.Any<ICommand<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var command = call.Arg<ICommand<Guid>>();
                _sent.Add(command);

                return reply(command);
            });
}