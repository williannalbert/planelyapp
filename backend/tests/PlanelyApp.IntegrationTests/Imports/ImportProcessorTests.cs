using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PlanelyApp.Application.Catalog.ServiceCategories.List;
using PlanelyApp.Application.Catalog.ServiceOfferings;
using PlanelyApp.Application.Catalog.ServiceOfferings.Create;
using PlanelyApp.Application.Catalog.ServiceOfferings.List;
using PlanelyApp.Application.Imports;
using PlanelyApp.Application.Imports.Get;
using PlanelyApp.Application.Imports.Prepare;
using PlanelyApp.Application.Imports.RecordRow;
using PlanelyApp.Application.Imports.Request;
using PlanelyApp.Application.Organization.Customers.List;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Imports;
using PlanelyApp.Infrastructure.Imports;
using PlanelyApp.IntegrationTests.Messaging;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;
using static PlanelyApp.IntegrationTests.Imports.XlsxBuilder;

namespace PlanelyApp.IntegrationTests.Imports;

[Collection(ImportCollection.Name)]
public class ImportProcessorTests(MySqlFixture mysql)
{
    private static readonly Guid UserId = Guid.CreateVersion7();

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_valid_services_file_should_create_the_services_and_share_the_category()
    {
        var company = await NewCompanyAsync();
        var id = await RequestAsync(company, ImportType.Services, Build(
            Row("Name", "Duration", "Price", "Category"),
            Row("Haircut", 30, 50.5, "Hair"),
            Row("Beard", 20, "R$ 30,00", "hair"),
            Row("Consultation", 15, null, null)));

        await Processor().RunAsync(_ct);

        var import = await GetAsync(company, id);
        import.Status.ShouldBe(ImportStatus.Completed);
        import.TotalRows.ShouldBe(3);
        import.SuccessRows.ShouldBe(3);
        import.ErrorRows.ShouldBe(0);
        import.CompletedAtUtc.ShouldNotBeNull();

        var services = await ServicesAsync(company);
        var haircut = services.Single(service => service.Name == "Haircut");
        haircut.PricingType.ShouldBe(PricingType.Fixed);
        haircut.BasePrice.ShouldBe(50.5m);
        haircut.CategoryName.ShouldBe("Hair");
        var beard = services.Single(service => service.Name == "Beard");
        beard.BasePrice.ShouldBe(30m);
        beard.CategoryId.ShouldBe(haircut.CategoryId); // "hair" found the category the first row created
        var consultation = services.Single(service => service.Name == "Consultation");
        consultation.PricingType.ShouldBe(PricingType.OnRequest);
        consultation.CategoryId.ShouldBeNull();

        (await mysql.QueryAsync(company, new ListServiceCategoriesQuery(), _ct)).Value.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Rows_that_fail_should_be_errors_of_the_import_and_the_others_still_imported()
    {
        var company = await NewCompanyAsync();
        var id = await RequestAsync(company, ImportType.Services, Build(
            Row("Name", "Duration", "Price"),
            Row("Haircut", 30, 50),
            Row("Beard", "abc", null),
            Row(null, 30, null),
            Row("Haircut", 45, null),
            Row("Wax", 30, "1.2.3"),
            Row("Shave", 20, -5)));

        await Processor().RunAsync(_ct);

        var import = await GetAsync(company, id);
        import.Status.ShouldBe(ImportStatus.CompletedWithErrors);
        import.TotalRows.ShouldBe(6);
        import.SuccessRows.ShouldBe(1);
        import.ErrorRows.ShouldBe(5);
        import.Errors.Select(error => (error.Row, error.Reason)).Take(4).ToList().ShouldBe(
        [
            (3, "Import.ValueInvalid(duration)"),
            (4, "Import.ValueRequired(name)"),
            (5, "ServiceOffering.NameAlreadyExists"),
            (6, "Import.ValueInvalid(price)"),
        ]);
        import.Errors[4].Row.ShouldBe(7); // a negative price: the code is the Domain's
        import.Errors[4].Reason.ShouldNotBeNullOrWhiteSpace();
        (await ServicesAsync(company)).Select(service => service.Name).ShouldBe(["Haircut"]);
    }

    [Fact]
    public async Task A_customers_file_should_register_the_customers_and_report_the_rows_that_fail()
    {
        var company = await NewCompanyAsync();
        var id = await RequestAsync(company, ImportType.Customers, Build(
            Row("Name", "Email", "Phone", "Marketing", "Language"),
            Row("Ana", "ana@example.com", 5511999990001L, "sim", "pt-BR"),
            Row("Bruno", "bruno@example.com", "+55 11 99999-0002", "não", null),
            Row("Carla", "ana@example.com", null, null, null),
            Row("Diego", null, null, null, null),
            Row("Eva", "eva@example.com", null, "talvez", null)));

        await Processor().RunAsync(_ct);

        var import = await GetAsync(company, id);
        import.Status.ShouldBe(ImportStatus.CompletedWithErrors);
        import.SuccessRows.ShouldBe(2);
        import.Errors.Select(error => (error.Row, error.Reason)).ToList().ShouldBe(
        [
            (4, "Customer.EmailAlreadyRegistered"),
            (5, "Customer.ContactRequired"),
            (6, "Import.ValueInvalid(marketing)"),
        ]);

        var customers = (await mysql.QueryAsync(company, new ListCustomersQuery(PageSize: 100), _ct)).Value.Items;
        customers.Select(customer => customer.Name).ShouldBe(["Ana", "Bruno"], ignoreOrder: true);
        var ana = customers.Single(customer => customer.Name == "Ana");
        ana.Phone.ShouldBe("+5511999990001");
        ana.AcceptsMarketing.ShouldBeTrue();
        customers.Single(customer => customer.Name == "Bruno").AcceptsMarketing.ShouldBeFalse();
    }

    [Fact]
    public async Task A_file_that_is_not_a_spreadsheet_should_fail_the_import()
    {
        var company = await NewCompanyAsync();
        var id = await RequestAsync(company, ImportType.Services, [1, 2, 3, 4]);

        await Processor().RunAsync(_ct);

        var import = await GetAsync(company, id);
        import.Status.ShouldBe(ImportStatus.Failed);
        import.FailureReason.ShouldBe("Import.FileUnreadable");
        import.TotalRows.ShouldBe(0);
    }

    [Fact]
    public async Task A_file_without_a_required_column_should_fail_the_import_and_create_nothing()
    {
        var company = await NewCompanyAsync();
        var id = await RequestAsync(company, ImportType.Services, Build(Row("Name", "Price"), Row("Haircut", 50)));

        await Processor().RunAsync(_ct);

        var import = await GetAsync(company, id);
        import.Status.ShouldBe(ImportStatus.Failed);
        import.FailureReason.ShouldBe("Import.MissingColumn(duration)");
        (await ServicesAsync(company)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_file_that_is_gone_from_the_store_should_fail_the_import()
    {
        var company = await NewCompanyAsync();
        var import = DataImport.Request(
            company, ImportType.Services, "gone.xlsx", $"{company:N}/gone.xlsx", UserId, DateTime.UtcNow);
        await mysql.SaveAsync(company, _ct, services => services.GetRequiredService<IDataImportRepository>().Add(import));

        await Processor().RunAsync(_ct);

        var saved = await GetAsync(company, import.Id);
        saved.Status.ShouldBe(ImportStatus.Failed);
        saved.FailureReason.ShouldBe("Import.FileMissing");
    }

    [Fact]
    public async Task An_interrupted_import_should_resume_after_the_rows_already_counted()
    {
        var company = await NewCompanyAsync();
        var id = await RequestAsync(company, ImportType.Services, Build(
            Row("Name", "Duration"), Row("A", 10), Row("B", 10), Row("C", 10)));

        // What a crash leaves: the import started, row 2 imported and counted, and nothing else.
        (await mysql.SendAsync(company, new PrepareImportCommand(id), _ct)).IsSuccess.ShouldBeTrue();
        (await mysql.SendAsync(
            company, new CreateServiceOfferingCommand("A", 10, new PricingData(PricingType.OnRequest)), _ct))
            .IsSuccess.ShouldBeTrue();
        (await mysql.SendAsync(company, new RecordImportRowCommand(id, 2, null), _ct)).IsSuccess.ShouldBeTrue();

        await Processor().RunAsync(_ct);

        // If row 2 had been imported again, it would be an error ("already exists").
        var import = await GetAsync(company, id);
        import.Status.ShouldBe(ImportStatus.Completed);
        import.SuccessRows.ShouldBe(3);
        import.ErrorRows.ShouldBe(0);
        (await ServicesAsync(company)).Select(service => service.Name).ShouldBe(["A", "B", "C"], ignoreOrder: true);
    }

    [Fact]
    public async Task One_pass_should_handle_the_imports_of_two_companies_each_in_its_own()
    {
        var first = await NewCompanyAsync();
        var second = await NewCompanyAsync();
        var firstId = await RequestAsync(first, ImportType.Services, Build(Row("Name", "Duration"), Row("Alpha", 10)));
        var secondId = await RequestAsync(second, ImportType.Services, Build(Row("Name", "Duration"), Row("Alpha", 20)));

        await Processor().RunAsync(_ct);

        (await GetAsync(first, firstId)).Status.ShouldBe(ImportStatus.Completed);
        (await GetAsync(second, secondId)).Status.ShouldBe(ImportStatus.Completed);
        (await ServicesAsync(first)).Single().DurationMinutes.ShouldBe(10);
        (await ServicesAsync(second)).Single().DurationMinutes.ShouldBe(20);
    }

    [Fact]
    public async Task A_finished_import_should_not_be_touched_by_the_next_pass()
    {
        var company = await NewCompanyAsync();
        var id = await RequestAsync(company, ImportType.Services, Build(Row("Name", "Duration"), Row("Haircut", 30)));
        await Processor().RunAsync(_ct);
        var first = await GetAsync(company, id);

        await Processor().RunAsync(_ct);

        var second = await GetAsync(company, id);
        second.Status.ShouldBe(ImportStatus.Completed);
        second.CompletedAtUtc.ShouldBe(first.CompletedAtUtc);
        second.SuccessRows.ShouldBe(1);
        (await ServicesAsync(company)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task The_worker_should_find_a_new_import_on_its_own()
    {
        var company = await NewCompanyAsync();
        var worker = new ImportWorkerService(
            Processor(),
            new ImportOptions { PollingInterval = TimeSpan.FromMilliseconds(100), BatchSize = 1000 },
            TimeProvider.System,
            NullLogger<ImportWorkerService>.Instance);

        await worker.StartAsync(_ct);

        try
        {
            var id = await RequestAsync(company, ImportType.Services, Build(Row("Name", "Duration"), Row("Haircut", 30)));

            await Eventually.UntilAsync(
                async () => (await GetAsync(company, id)).Status == ImportStatus.Completed, _ct, timeoutSeconds: 30);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        (await ServicesAsync(company)).Single().Name.ShouldBe("Haircut");
    }

    // A singleton: taking it from a scope is only a way to reach the provider of the fixture.
    private ImportProcessor Processor()
    {
        using var scope = mysql.CreateScope();

        return scope.ServiceProvider.GetRequiredService<ImportProcessor>();
    }

    private async Task<Guid> NewCompanyAsync() => (await OrganizationData.SeedCompanyAsync(mysql, _ct)).Id;

    private async Task<Guid> RequestAsync(Guid company, ImportType type, byte[] bytes)
    {
        using var content = new MemoryStream(bytes);
        var requested = await mysql.SendAsStaffAsync(
            company, UserId, new RequestImportCommand(type, "file.xlsx", content), _ct);

        requested.IsSuccess.ShouldBeTrue();

        return requested.Value;
    }

    private async Task<DataImportDto> GetAsync(Guid company, Guid id) =>
        (await mysql.QueryAsync(company, new GetImportQuery(id), _ct)).Value;

    private async Task<IReadOnlyList<ServiceOfferingSummaryDto>> ServicesAsync(Guid company) =>
        (await mysql.QueryAsync(company, new ListServiceOfferingsQuery(PageSize: 100), _ct)).Value.Items;
}