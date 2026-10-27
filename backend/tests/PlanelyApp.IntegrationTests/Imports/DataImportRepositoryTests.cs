using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.Imports;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Imports;

public class DataImportRepositoryTests(MySqlFixture mysql)
{
    private static readonly DateTime Now = DateTime.UtcNow.Date.AddHours(8);
    private static readonly Guid Actor = Guid.CreateVersion7();

    private static DataImport NewImport(Guid companyId, ImportType type = ImportType.Services) =>
        DataImport.Request(companyId, type, "services.xlsx", $"imports/{Guid.NewGuid():N}/services.xlsx", Actor, Now);

    [Fact]
    public async Task A_requested_import_should_come_back_pending()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var import = NewImport(company.Id, ImportType.Customers);
        await SaveAsync(company.Id, import, cancellationToken);

        var loaded = await LoadAsync(company.Id, import.Id, cancellationToken);

        loaded.ShouldNotBeNull();
        loaded.CompanyId.ShouldBe(company.Id);
        loaded.Type.ShouldBe(ImportType.Customers);
        loaded.FileName.ShouldBe("services.xlsx");
        loaded.StoragePath.ShouldBe(import.StoragePath);
        loaded.Status.ShouldBe(ImportStatus.Pending);
        loaded.TotalRows.ShouldBe(0);
        loaded.RequestedBy.ShouldBe(Actor);
        loaded.CreatedAtUtc.ShouldBe(Now);
        loaded.CreatedAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
        loaded.CompletedAtUtc.ShouldBeNull();
        loaded.FailureReason.ShouldBeNull();
        loaded.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_progress_saved_in_batches_should_add_up_to_a_completed_import_with_errors()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var import = NewImport(company.Id);
        await SaveAsync(company.Id, import, cancellationToken);

        await UpdateAsync(company.Id, import.Id, cancellationToken, loaded => loaded.Start(3));
        await UpdateAsync(company.Id, import.Id, cancellationToken, loaded =>
        {
            loaded.RecordSuccess();
            loaded.RecordError(2, "Invalid price");
        });

        var processing = await LoadAsync(company.Id, import.Id, cancellationToken);

        processing.ShouldNotBeNull();
        processing.Status.ShouldBe(ImportStatus.Processing);
        processing.TotalRows.ShouldBe(3);
        processing.SuccessRows.ShouldBe(1);
        processing.ErrorRows.ShouldBe(1);
        processing.ProcessedRows.ShouldBe(2);

        var error = processing.Errors.ShouldHaveSingleItem();
        error.Row.ShouldBe(2);
        error.Reason.ShouldBe("Invalid price");

        await UpdateAsync(company.Id, import.Id, cancellationToken, loaded =>
        {
            loaded.RecordSuccess();
            loaded.Complete(Now.AddMinutes(5));
        });

        var completed = await LoadAsync(company.Id, import.Id, cancellationToken);

        completed.ShouldNotBeNull();
        completed.Status.ShouldBe(ImportStatus.CompletedWithErrors);
        completed.ProcessedRows.ShouldBe(3);
        completed.CompletedAtUtc.ShouldBe(Now.AddMinutes(5));
        completed.Errors.ShouldHaveSingleItem().Row.ShouldBe(2);
        completed.ErrorsAreTruncated.ShouldBeFalse();
    }

    [Fact]
    public async Task An_import_without_errors_should_complete_cleanly()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var import = NewImport(company.Id);
        await SaveAsync(company.Id, import, cancellationToken);

        await UpdateAsync(company.Id, import.Id, cancellationToken, loaded =>
        {
            loaded.Start(2);
            loaded.RecordSuccess();
            loaded.RecordSuccess();
            loaded.Complete(Now.AddMinutes(1));
        });

        var completed = await LoadAsync(company.Id, import.Id, cancellationToken);

        completed.ShouldNotBeNull();
        completed.Status.ShouldBe(ImportStatus.Completed);
        completed.SuccessRows.ShouldBe(2);
        completed.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_failed_import_should_keep_the_reason_and_the_rows_already_counted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var import = NewImport(company.Id);
        await SaveAsync(company.Id, import, cancellationToken);

        await UpdateAsync(company.Id, import.Id, cancellationToken, loaded =>
        {
            loaded.Start(10);
            loaded.RecordSuccess();
            loaded.Fail("The file is not a spreadsheet", Now.AddMinutes(1));
        });

        var failed = await LoadAsync(company.Id, import.Id, cancellationToken);

        failed.ShouldNotBeNull();
        failed.Status.ShouldBe(ImportStatus.Failed);
        failed.FailureReason.ShouldBe("The file is not a spreadsheet");
        failed.SuccessRows.ShouldBe(1);
        failed.CompletedAtUtc.ShouldBe(Now.AddMinutes(1));
    }

    [Fact]
    public async Task Only_the_first_errors_should_be_stored_but_all_of_them_counted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var import = NewImport(company.Id);
        await SaveAsync(company.Id, import, cancellationToken);

        const int totalErrors = DataImport.MaxStoredErrors + 200;

        await UpdateAsync(company.Id, import.Id, cancellationToken, loaded =>
        {
            loaded.Start(totalErrors);

            for (var row = 1; row <= totalErrors; row++)
            {
                loaded.RecordError(row, $"Line {row} is wrong");
            }

            loaded.Complete(Now.AddMinutes(1));
        });

        var loadedAgain = await LoadAsync(company.Id, import.Id, cancellationToken);

        loadedAgain.ShouldNotBeNull();
        loadedAgain.ErrorRows.ShouldBe(totalErrors);
        loadedAgain.Errors.Count.ShouldBe(DataImport.MaxStoredErrors);
        loadedAgain.ErrorsAreTruncated.ShouldBeTrue();
        loadedAgain.Errors.Select(error => error.Row).Order().ShouldBe(Enumerable.Range(1, DataImport.MaxStoredErrors));
    }

    [Fact]
    public async Task The_longest_values_the_domain_allows_should_fit_in_the_columns()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var import = DataImport.Request(
            company.Id, ImportType.Services, new string('F', 255), new string('P', 500), Actor, Now);
        await SaveAsync(company.Id, import, cancellationToken);

        await UpdateAsync(company.Id, import.Id, cancellationToken, loaded =>
        {
            loaded.Start(2);
            loaded.RecordError(1, new string('R', 500));
            loaded.Fail(new string('X', 500), Now.AddMinutes(1));
        });

        var loadedAgain = await LoadAsync(company.Id, import.Id, cancellationToken);

        loadedAgain.ShouldNotBeNull();
        loadedAgain.FileName.Length.ShouldBe(255);
        loadedAgain.StoragePath.Length.ShouldBe(500);
        loadedAgain.FailureReason!.Length.ShouldBe(500);
        loadedAgain.Errors.ShouldHaveSingleItem().Reason.Length.ShouldBe(500);
    }

    [Fact]
    public async Task An_import_should_only_be_found_by_its_own_company()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var import = NewImport(company.Id);
        await SaveAsync(company.Id, import, cancellationToken);

        (await LoadAsync(company.Id, import.Id, cancellationToken)).ShouldNotBeNull();
        (await LoadAsync(other.Id, import.Id, cancellationToken)).ShouldBeNull();
        (await LoadAsync(null, import.Id, cancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Two_jobs_should_not_both_start_the_same_import()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var import = NewImport(company.Id);
        await SaveAsync(company.Id, import, cancellationToken);

        await using var first = mysql.CreateScope(company.Id);
        await using var second = mysql.CreateScope(company.Id);

        var seenByFirst = await first.ServiceProvider.GetRequiredService<IDataImportRepository>().GetByIdAsync(import.Id, cancellationToken);
        var seenBySecond = await second.ServiceProvider.GetRequiredService<IDataImportRepository>().GetByIdAsync(import.Id, cancellationToken);

        // Both read "Pending", so both are allowed to start it in memory.
        seenByFirst!.Start(5);
        seenBySecond!.Start(5);

        await first.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);

        await Should.ThrowAsync<DbUpdateConcurrencyException>(
            () => second.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken));
    }

    private Task SaveAsync(Guid companyId, DataImport import, CancellationToken cancellationToken) =>
        mysql.SaveAsync(companyId, cancellationToken,
            services => services.GetRequiredService<IDataImportRepository>().Add(import));

    private async Task UpdateAsync(Guid companyId, Guid id, CancellationToken cancellationToken, Action<DataImport> change)
    {
        await using var scope = mysql.CreateScope(companyId);
        var loaded = await scope.ServiceProvider.GetRequiredService<IDataImportRepository>().GetByIdAsync(id, cancellationToken);

        change(loaded!);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
    }

    private async Task<DataImport?> LoadAsync(Guid? companyId, Guid id, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);

        return await scope.ServiceProvider.GetRequiredService<IDataImportRepository>().GetByIdAsync(id, cancellationToken);
    }
}