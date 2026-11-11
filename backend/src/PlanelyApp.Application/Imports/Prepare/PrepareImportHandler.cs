using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Imports;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Imports.Processing;
using PlanelyApp.Domain.Imports;

namespace PlanelyApp.Application.Imports.Prepare;

internal sealed class PrepareImportHandler(
    IDataImportRepository imports,
    IImportFileStore files,
    IImportSheetReader reader,
    TimeProvider timeProvider)
    : ICommandHandler<PrepareImportCommand, PrepareImportResponse>
{
    public async Task<Result<PrepareImportResponse>> HandleAsync(
        PrepareImportCommand command,
        CancellationToken cancellationToken)
    {
        var import = await imports.GetByIdAsync(command.ImportId, cancellationToken);

        if (import is null)
        {
            return Error.NotFound("DataImport.NotFound");
        }

        // Finished by an earlier run: a stale list, not an error.
        if (import.Status is not (ImportStatus.Pending or ImportStatus.Processing))
        {
            return new PrepareImportResponse(null);
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        ImportSheet sheet;

        try
        {
            await using var content = await files.OpenReadAsync(import.StoragePath, cancellationToken);
            sheet = await reader.ReadAsync(content, ImportRules.MaxRows, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return Fail(import, ImportReasons.Of(ImportReasons.FileMissing), nowUtc);
        }
        catch (ImportSheetException)
        {
            return Fail(import, ImportReasons.Of(ImportReasons.FileUnreadable), nowUtc);
        }

        if (sheet.Rows.Count == 0)
        {
            return Fail(import, ImportReasons.Of(ImportReasons.NoRows), nowUtc);
        }

        if (sheet.Rows.Count > ImportRules.MaxRows)
        {
            return Fail(import, ImportReasons.Of(ImportReasons.TooManyRows, ImportRules.MaxRows), nowUtc);
        }

        var map = ImportSchemas.MapHeaders(sheet.Headers);

        if (ImportSchemas.FirstMissingColumn(import.Type, map) is { } missing)
        {
            return Fail(import, ImportReasons.Of(ImportReasons.MissingColumn, missing), nowUtc);
        }

        if (import.Status == ImportStatus.Pending)
        {
            import.Start(sheet.Rows.Count);
        }
        else if (import.TotalRows != sheet.Rows.Count)
        {
            // A resumed import counts rows by position: a file with another size cannot be resumed.
            return Fail(import, ImportReasons.Of(ImportReasons.FileChanged), nowUtc);
        }

        return new PrepareImportResponse(new ImportWork(import.Type, import.ProcessedRows, map, sheet.Rows));
    }

    // A failed import is returned as a success of the use case, on purpose: the transaction only saves a success,
    // and the failure of the import must be saved.
    private static Result<PrepareImportResponse> Fail(DataImport import, string reason, DateTime nowUtc)
    {
        import.Fail(reason, nowUtc);

        return new PrepareImportResponse(null);
    }
}