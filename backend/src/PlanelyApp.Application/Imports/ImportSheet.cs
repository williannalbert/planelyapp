namespace PlanelyApp.Application.Abstractions.Imports;

/// <param name="Headers">The texts of the header row.</param>
/// <param name="Rows">The rows below it, with no blank row.</param>
public sealed record ImportSheet(IReadOnlyList<string> Headers, IReadOnlyList<ImportSheetRow> Rows);

/// <param name="Number">The line as the user sees it in the spreadsheet, starting at 1 (the header is usually 1).</param>
/// <param name="Cells">As many texts as there are headers; a missing cell is an empty text.</param>
public sealed record ImportSheetRow(int Number, IReadOnlyList<string> Cells);