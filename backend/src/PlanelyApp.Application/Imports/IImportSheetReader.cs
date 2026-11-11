namespace PlanelyApp.Application.Abstractions.Imports;

/// <summary>
/// Reads the first worksheet of a spreadsheet. A port: the Application does not know the library that opens the file.
/// </summary>
public interface IImportSheetReader
{
    /// <summary>
    /// The first non-blank row is the header; blank rows are skipped. It stops after <paramref name="maxRows"/> + 1 rows,
    /// so a huge file is not read to the end: the caller only needs to know that there are too many.
    /// </summary>
    /// <exception cref="ImportSheetException">The content is not a spreadsheet the reader can open.</exception>
    Task<ImportSheet> ReadAsync(Stream content, int maxRows, CancellationToken cancellationToken);
}