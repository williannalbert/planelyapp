using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Imports.RecordRow;
using PlanelyApp.Domain.Imports;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests.Imports;

public class RecordImportRowTests
{
    private static readonly DateTime Now = new(2027, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    private readonly IDataImportRepository _imports = Substitute.For<IDataImportRepository>();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_row_without_a_reason_should_count_as_a_success()
    {
        var import = ProcessingImport();

        var result = await Handler().HandleAsync(new RecordImportRowCommand(import.Id, 2, null), _ct);

        result.IsSuccess.ShouldBeTrue();
        import.SuccessRows.ShouldBe(1);
        import.ErrorRows.ShouldBe(0);
    }

    [Fact]
    public async Task A_row_with_a_reason_should_count_as_an_error_and_keep_the_reason()
    {
        var import = ProcessingImport();

        var result = await Handler().HandleAsync(
            new RecordImportRowCommand(import.Id, 3, "Import.ValueInvalid(duration)"), _ct);

        result.IsSuccess.ShouldBeTrue();
        import.ErrorRows.ShouldBe(1);
        var error = import.Errors.Single();
        error.Row.ShouldBe(3);
        error.Reason.ShouldBe("Import.ValueInvalid(duration)");
    }

    [Fact]
    public async Task An_import_that_does_not_exist_should_be_not_found()
    {
        var result = await Handler().HandleAsync(new RecordImportRowCommand(Guid.CreateVersion7(), 2, null), _ct);

        result.Error.Code.ShouldBe("DataImport.NotFound");
    }

    [Fact]
    public async Task An_import_that_is_not_processing_should_be_refused_by_the_domain()
    {
        var import = NewImport();
        _imports.GetByIdAsync(import.Id, Arg.Any<CancellationToken>()).Returns(import);

        var exception = await Should.ThrowAsync<DomainException>(
            () => Handler().HandleAsync(new RecordImportRowCommand(import.Id, 2, null), _ct));

        exception.Code.ShouldBe("DataImport.NotProcessing");
    }

    private RecordImportRowHandler Handler() => new(_imports);

    private DataImport ProcessingImport()
    {
        var import = NewImport();
        import.Start(2);
        _imports.GetByIdAsync(import.Id, Arg.Any<CancellationToken>()).Returns(import);

        return import;
    }

    private static DataImport NewImport() => DataImport.Request(
        Guid.CreateVersion7(), ImportType.Services, "services.xlsx", "company/file.xlsx", Guid.CreateVersion7(), Now);
}