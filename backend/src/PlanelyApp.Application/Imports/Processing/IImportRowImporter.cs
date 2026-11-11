using PlanelyApp.Application.Abstractions.Imports;

namespace PlanelyApp.Application.Imports.Processing;

/// <summary>
/// Imports one row through the use cases a person would use. It runs inside a scope that already has the company, and a
/// row that cannot be imported is a result, not an exception: one bad row never stops the file.
/// </summary>
public interface IImportRowImporter
{
    Task<ImportRowResult> ImportAsync(ImportWork work, ImportSheetRow row, CancellationToken cancellationToken);
}