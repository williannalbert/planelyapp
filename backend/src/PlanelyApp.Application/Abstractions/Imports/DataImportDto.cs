using PlanelyApp.Domain.Imports;

namespace PlanelyApp.Application.Imports;

/// <summary>An import with its progress and its errors. The path of the file stays inside: it is not for the client.</summary>
public sealed record DataImportDto(
    Guid Id,
    ImportType Type,
    string FileName,
    ImportStatus Status,
    int TotalRows,
    int ProcessedRows,
    int SuccessRows,
    int ErrorRows,
    bool ErrorsAreTruncated,
    string? FailureReason,
    Guid RequestedBy,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    IReadOnlyList<ImportErrorDto> Errors);

/// <summary>A row that was not imported. The reason is a code, which the Api translates; the row is the line of the spreadsheet.</summary>
public sealed record ImportErrorDto(int Row, string Reason);