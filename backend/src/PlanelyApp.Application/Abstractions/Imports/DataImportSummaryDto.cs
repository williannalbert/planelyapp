using PlanelyApp.Domain.Imports;

namespace PlanelyApp.Application.Imports;

/// <summary>What a list needs: the progress, not the errors (a list of 1000 errors per line would not be a list).</summary>
public sealed record DataImportSummaryDto(
    Guid Id,
    ImportType Type,
    string FileName,
    ImportStatus Status,
    int TotalRows,
    int ProcessedRows,
    int ErrorRows,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc);