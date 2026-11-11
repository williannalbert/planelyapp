using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PlanelyApp.Application.Abstractions.Imports;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Imports;
using PlanelyApp.Application.Imports.Prepare;
using PlanelyApp.Application.Tests.Scheduling;
using PlanelyApp.Domain.Imports;
using Shouldly;

namespace PlanelyApp.Application.Tests.Imports;

public class PrepareImportTests
{
    private static readonly DateTime Now = new(2027, 11, 15, 12, 0, 0, DateTimeKind.Utc);
    private const string StoragePath = "company/file.xlsx";
    private static readonly string[] ServiceHeaders = ["Name", "Duration"];
    private static readonly string[] CustomerHeaders = ["Name", "Email"];

    private readonly IDataImportRepository _imports = Substitute.For<IDataImportRepository>();
    private readonly IImportFileStore _files = Substitute.For<IImportFileStore>();
    private readonly IImportSheetReader _reader = Substitute.For<IImportSheetReader>();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public PrepareImportTests() =>
        _files.OpenReadAsync(StoragePath, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream>(new MemoryStream()));

    [Fact]
    public async Task A_valid_services_file_should_start_the_import_with_the_total_of_rows()
    {
        var import = NewImport(ImportType.Services);
        Reads(ServiceHeaders, ["Haircut", "30"], ["Beard", "20"]);

        var result = await HandleAsync(import);

        result.IsSuccess.ShouldBeTrue();
        import.Status.ShouldBe(ImportStatus.Processing);
        import.TotalRows.ShouldBe(2);
        var work = result.Value.Work!;
        work.Type.ShouldBe(ImportType.Services);
        work.ProcessedRows.ShouldBe(0);
        work.Rows.Select(row => row.Number).ShouldBe([2, 3]);
    }

    [Fact]
    public async Task A_valid_customers_file_should_start_the_import()
    {
        var import = NewImport(ImportType.Customers);
        Reads(CustomerHeaders, ["Ana", "ana@example.com"]);

        var result = await HandleAsync(import);

        import.Status.ShouldBe(ImportStatus.Processing);
        result.Value.Work!.Type.ShouldBe(ImportType.Customers);
    }

    [Fact]
    public async Task A_file_that_is_gone_should_fail_the_import_and_save_the_failure()
    {
        var import = NewImport(ImportType.Services);
        _files.OpenReadAsync(StoragePath, Arg.Any<CancellationToken>()).ThrowsAsync(new FileNotFoundException());

        var result = await HandleAsync(import);

        AssertFailed(import, result, "Import.FileMissing");
    }

    [Fact]
    public async Task A_file_that_is_not_a_spreadsheet_should_fail_the_import()
    {
        var import = NewImport(ImportType.Services);
        _reader.ReadAsync(Arg.Any<Stream>(), ImportRules.MaxRows, Arg.Any<CancellationToken>())
            .ThrowsAsync(new ImportSheetException("not a spreadsheet"));

        var result = await HandleAsync(import);

        AssertFailed(import, result, "Import.FileUnreadable");
    }

    [Fact]
    public async Task A_file_with_only_the_header_should_fail_the_import()
    {
        var import = NewImport(ImportType.Services);
        Reads(ServiceHeaders);

        var result = await HandleAsync(import);

        AssertFailed(import, result, "Import.NoRows");
    }

    [Fact]
    public async Task A_file_with_too_many_rows_should_fail_the_import()
    {
        var import = NewImport(ImportType.Services);
        _reader.ReadAsync(Arg.Any<Stream>(), ImportRules.MaxRows, Arg.Any<CancellationToken>())
            .Returns(new ImportSheet(
                ServiceHeaders,
                Enumerable.Range(0, ImportRules.MaxRows + 1)
                    .Select(index => new ImportSheetRow(index + 2, ["Service " + index, "30"]))
                    .ToList()));

        var result = await HandleAsync(import);

        AssertFailed(import, result, "Import.TooManyRows(5000)");
    }

    [Fact]
    public async Task A_services_file_without_the_duration_should_fail_the_import()
    {
        var import = NewImport(ImportType.Services);
        Reads(["Name", "Price"], ["Haircut", "50"]);

        var result = await HandleAsync(import);

        AssertFailed(import, result, "Import.MissingColumn(duration)");
    }

    [Fact]
    public async Task A_customers_file_with_neither_email_nor_phone_should_fail_the_import()
    {
        var import = NewImport(ImportType.Customers);
        Reads(["Name"], ["Ana"]);

        var result = await HandleAsync(import);

        AssertFailed(import, result, "Import.MissingColumn(email|phone)");
    }

    [Fact]
    public async Task An_import_that_does_not_exist_should_be_not_found()
    {
        var result = await new PrepareImportHandler(_imports, _files, _reader, new FixedTimeProvider(Now))
            .HandleAsync(new PrepareImportCommand(Guid.CreateVersion7()), _ct);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("DataImport.NotFound");
    }

    [Fact]
    public async Task An_import_that_is_already_finished_should_have_no_work_and_its_file_stay_closed()
    {
        var import = NewImport(ImportType.Services);
        import.Start(0);
        import.Complete(Now);

        var result = await HandleAsync(import);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Work.ShouldBeNull();
        await _files.DidNotReceiveWithAnyArgs().OpenReadAsync(default!, _ct);
    }

    [Fact]
    public async Task An_import_in_progress_should_resume_after_the_rows_already_counted()
    {
        var import = NewImport(ImportType.Services);
        import.Start(3);
        import.RecordSuccess();
        Reads(ServiceHeaders, ["A", "10"], ["B", "10"], ["C", "10"]);

        var result = await HandleAsync(import);

        import.Status.ShouldBe(ImportStatus.Processing);
        result.Value.Work!.ProcessedRows.ShouldBe(1);
        result.Value.Work.Rows.Count.ShouldBe(3);
    }

    [Fact]
    public async Task An_import_in_progress_whose_file_changed_size_should_fail()
    {
        var import = NewImport(ImportType.Services);
        import.Start(5);
        Reads(ServiceHeaders, ["A", "10"], ["B", "10"], ["C", "10"]);

        var result = await HandleAsync(import);

        AssertFailed(import, result, "Import.FileChanged");
    }

    private DataImport NewImport(ImportType type)
    {
        var import = DataImport.Request(
            Guid.CreateVersion7(), type, "file.xlsx", StoragePath, Guid.CreateVersion7(), Now.AddMinutes(-5));
        _imports.GetByIdAsync(import.Id, Arg.Any<CancellationToken>()).Returns(import);

        return import;
    }

    private void Reads(string[] headers, params string[][] rows) =>
        _reader.ReadAsync(Arg.Any<Stream>(), ImportRules.MaxRows, Arg.Any<CancellationToken>())
            .Returns(new ImportSheet(
                headers,
                rows.Select((cells, index) => new ImportSheetRow(index + 2, cells)).ToList()));

    private Task<Result<PrepareImportResponse>> HandleAsync(DataImport import) =>
        new PrepareImportHandler(_imports, _files, _reader, new FixedTimeProvider(Now))
            .HandleAsync(new PrepareImportCommand(import.Id), _ct);

    // A failed import is a success of the use case (so it is saved) with no work.
    private static void AssertFailed(DataImport import, Result<PrepareImportResponse> result, string reason)
    {
        result.IsSuccess.ShouldBeTrue();
        result.Value.Work.ShouldBeNull();
        import.Status.ShouldBe(ImportStatus.Failed);
        import.FailureReason.ShouldBe(reason);
        import.CompletedAtUtc.ShouldBe(Now);
    }
}