using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Imports.Get;
using PlanelyApp.Domain.Imports;
using Shouldly;

namespace PlanelyApp.Application.Tests.Imports;

public class GetImportTests
{
    private static readonly DateTime Now = new(2027, 11, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.CreateVersion7();

    private readonly IDataImportRepository _imports = Substitute.For<IDataImportRepository>();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_progress_and_the_errors_should_come_back()
    {
        var import = NewImport();
        import.Start(3);
        import.RecordSuccess();
        import.RecordError(2, "ServiceOffering.NameAlreadyExists");
        import.RecordError(3, "Validation.Invalid");
        import.Complete(Now.AddMinutes(1));
        Found(import);

        var result = await new GetImportHandler(_imports).HandleAsync(new GetImportQuery(import.Id), _ct);

        var dto = result.Value;
        dto.Id.ShouldBe(import.Id);
        dto.Type.ShouldBe(ImportType.Services);
        dto.FileName.ShouldBe("services.xlsx");
        dto.Status.ShouldBe(ImportStatus.CompletedWithErrors);
        dto.TotalRows.ShouldBe(3);
        dto.ProcessedRows.ShouldBe(3);
        dto.SuccessRows.ShouldBe(1);
        dto.ErrorRows.ShouldBe(2);
        dto.ErrorsAreTruncated.ShouldBeFalse();
        dto.FailureReason.ShouldBeNull();
        dto.RequestedBy.ShouldBe(UserId);
        dto.CreatedAtUtc.ShouldBe(Now);
        dto.CompletedAtUtc.ShouldBe(Now.AddMinutes(1));
        dto.Errors.Select(error => (error.Row, error.Reason)).ShouldBe(
            [(2, "ServiceOffering.NameAlreadyExists"), (3, "Validation.Invalid")]);
    }

    [Fact]
    public async Task More_errors_than_the_domain_keeps_should_say_the_list_is_truncated()
    {
        var import = NewImport();
        import.Start(DataImport.MaxStoredErrors + 1);

        for (var row = 1; row <= DataImport.MaxStoredErrors + 1; row++)
        {
            import.RecordError(row, "Validation.Invalid");
        }

        import.Complete(Now.AddMinutes(1));
        Found(import);

        var result = await new GetImportHandler(_imports).HandleAsync(new GetImportQuery(import.Id), _ct);

        result.Value.ErrorRows.ShouldBe(DataImport.MaxStoredErrors + 1);
        result.Value.Errors.Count.ShouldBe(DataImport.MaxStoredErrors);
        result.Value.ErrorsAreTruncated.ShouldBeTrue();
    }

    [Fact]
    public async Task A_missing_import_should_be_not_found()
    {
        _imports.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((DataImport?)null);

        var result = await new GetImportHandler(_imports).HandleAsync(new GetImportQuery(Guid.NewGuid()), _ct);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("DataImport.NotFound");
    }

    private static DataImport NewImport() =>
        DataImport.Request(Guid.NewGuid(), ImportType.Services, "services.xlsx", "company/file.xlsx", UserId, Now);

    private void Found(DataImport import) =>
        _imports.GetByIdAsync(import.Id, Arg.Any<CancellationToken>()).Returns(import);
}