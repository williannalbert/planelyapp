using PlanelyApp.Application.Abstractions.Imports;
using PlanelyApp.Domain.Imports;

namespace PlanelyApp.Application.Imports.Processing;

/// <summary>
/// What the job needs to import the rows of one file, given by <c>PrepareImportCommand</c>. The rows before
/// <see cref="ProcessedRows"/> were already counted by an earlier run, so the job starts after them.
/// </summary>
public sealed class ImportWork
{
    internal ImportWork(ImportType type, int processedRows, ImportColumnMap map, IReadOnlyList<ImportSheetRow> rows)
    {
        Type = type;
        ProcessedRows = processedRows;
        Map = map;
        Rows = rows;
    }

    public ImportType Type { get; }

    public int ProcessedRows { get; }

    public IReadOnlyList<ImportSheetRow> Rows { get; }

    internal ImportColumnMap Map { get; }
}