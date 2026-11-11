using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Imports.Get;
using PlanelyApp.Application.Imports.List;
using PlanelyApp.Application.Imports.Request;
using PlanelyApp.Domain.Imports;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Imports;

public class DataImportUseCaseTests(MySqlFixture mysql)
{
    private static readonly DateTime Now = DateTime.UtcNow.Date.AddHours(8);
    private static readonly byte[] Bytes = [1, 2, 3, 4];

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_staff_request_should_store_the_file_and_leave_a_pending_import()
    {
        var company = await NewCompanyAsync();
        var userId = Guid.CreateVersion7();
        using var content = new MemoryStream(Bytes);

        var requested = await mysql.SendAsStaffAsync(
            company, userId, new RequestImportCommand(ImportType.Services, "services.xlsx", content), _ct);

        requested.IsSuccess.ShouldBeTrue();
        var import = (await mysql.QueryAsync(company, new GetImportQuery(requested.Value), _ct)).Value;
        import.Status.ShouldBe(ImportStatus.Pending);
        import.Type.ShouldBe(ImportType.Services);
        import.FileName.ShouldBe("services.xlsx");
        import.RequestedBy.ShouldBe(userId);
        import.TotalRows.ShouldBe(0);
        import.Errors.ShouldBeEmpty();

        mysql.ImportFiles.CountFor(company).ShouldBe(1);
        await using var scope = mysql.CreateScope(company);
        var stored = (await scope.ServiceProvider.GetRequiredService<IDataImportRepository>()
            .GetByIdAsync(requested.Value, _ct))!;
        mysql.ImportFiles.ContentOf(stored.StoragePath).ShouldBe(Bytes);
    }

    [Fact]
    public async Task A_customer_should_be_refused_and_no_file_kept()
    {
        var company = await NewCompanyAsync();
        using var content = new MemoryStream(Bytes);

        var result = await mysql.SendAsCustomerAsync(
            company, Guid.CreateVersion7(), new RequestImportCommand(ImportType.Customers, "customers.xlsx", content), _ct);

        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("DataImport.StaffOnly");
        mysql.ImportFiles.CountFor(company).ShouldBe(0);
    }

    [Fact]
    public async Task A_file_that_is_not_a_xlsx_should_be_a_validation_error_and_no_file_kept()
    {
        var company = await NewCompanyAsync();
        using var content = new MemoryStream(Bytes);

        var result = await mysql.SendAsStaffAsync(
            company, Guid.CreateVersion7(), new RequestImportCommand(ImportType.Services, "services.csv", content), _ct);

        var failure = result.Error.ShouldBeOfType<ValidationError>().Failures.ShouldHaveSingleItem();
        failure.Field.ShouldBe("FileName");
        failure.Code.ShouldBe("Validation.Invalid");
        mysql.ImportFiles.CountFor(company).ShouldBe(0);
    }

    [Fact]
    public async Task An_import_should_not_be_seen_by_another_company()
    {
        var company = await NewCompanyAsync();
        var other = await NewCompanyAsync();
        var id = await SeedAsync(company, "services.xlsx", Now);

        var result = await mysql.QueryAsync(other, new GetImportQuery(id), _ct);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("DataImport.NotFound");
    }

    [Fact]
    public async Task A_finished_import_should_come_back_with_its_errors()
    {
        var company = await NewCompanyAsync();
        var id = await SeedAsync(company, "services.xlsx", Now, import =>
        {
            import.Start(3);
            import.RecordSuccess();
            import.RecordError(2, "ServiceOffering.NameAlreadyExists");
            import.RecordError(3, "Validation.Invalid");
            import.Complete(Now.AddMinutes(1));
        });

        var import = (await mysql.QueryAsync(company, new GetImportQuery(id), _ct)).Value;

        import.Status.ShouldBe(ImportStatus.CompletedWithErrors);
        import.ProcessedRows.ShouldBe(3);
        import.SuccessRows.ShouldBe(1);
        import.ErrorRows.ShouldBe(2);
        import.CompletedAtUtc.ShouldBe(Now.AddMinutes(1));
        import.Errors.Select(error => (error.Row, error.Reason)).ShouldBe(
            [(2, "ServiceOffering.NameAlreadyExists"), (3, "Validation.Invalid")], ignoreOrder: true);
    }

    [Fact]
    public async Task The_list_should_have_the_most_recent_first_and_only_the_imports_of_the_company()
    {
        var company = await NewCompanyAsync();
        var other = await NewCompanyAsync();
        var oldest = await SeedAsync(company, "a.xlsx", Now.AddHours(-3));
        var newest = await SeedAsync(company, "c.xlsx", Now.AddHours(-1));
        var middle = await SeedAsync(company, "b.xlsx", Now.AddHours(-2));
        await SeedAsync(other, "x.xlsx", Now);

        var page = (await mysql.QueryAsync(company, new ListImportsQuery(), _ct)).Value;

        page.TotalCount.ShouldBe(3);
        page.Items.Select(item => item.Id).ShouldBe([newest, middle, oldest]);
    }

    [Fact]
    public async Task The_list_should_be_paged()
    {
        var company = await NewCompanyAsync();
        await SeedAsync(company, "a.xlsx", Now.AddHours(-3));
        await SeedAsync(company, "b.xlsx", Now.AddHours(-2));
        var newest = await SeedAsync(company, "c.xlsx", Now.AddHours(-1));

        var first = (await mysql.QueryAsync(company, new ListImportsQuery(Page: 1, PageSize: 2), _ct)).Value;
        var second = (await mysql.QueryAsync(company, new ListImportsQuery(Page: 2, PageSize: 2), _ct)).Value;

        first.Items.Count.ShouldBe(2);
        first.Items[0].Id.ShouldBe(newest);
        first.TotalPages.ShouldBe(2);
        second.Items.Count.ShouldBe(1);
        second.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task The_list_should_show_the_progress_of_an_import_in_the_middle()
    {
        var company = await NewCompanyAsync();
        var id = await SeedAsync(company, "services.xlsx", Now, import =>
        {
            import.Start(3);
            import.RecordSuccess();
            import.RecordError(2, "Validation.Invalid");
        });

        var item = (await mysql.QueryAsync(company, new ListImportsQuery(), _ct)).Value.Items.Single();

        item.Id.ShouldBe(id);
        item.Status.ShouldBe(ImportStatus.Processing);
        item.TotalRows.ShouldBe(3);
        item.ProcessedRows.ShouldBe(2);
        item.ErrorRows.ShouldBe(1);
        item.CompletedAtUtc.ShouldBeNull();
    }

    private async Task<Guid> NewCompanyAsync() => (await OrganizationData.SeedCompanyAsync(mysql, _ct)).Id;

    private async Task<Guid> SeedAsync(Guid company, string fileName, DateTime createdAtUtc, Action<DataImport>? change = null)
    {
        var import = DataImport.Request(
            company, ImportType.Services, fileName, $"{company:N}/{Guid.NewGuid():N}.xlsx", Guid.CreateVersion7(), createdAtUtc);
        change?.Invoke(import);

        await mysql.SaveAsync(company, _ct, services => services.GetRequiredService<IDataImportRepository>().Add(import));

        return import.Id;
    }
}