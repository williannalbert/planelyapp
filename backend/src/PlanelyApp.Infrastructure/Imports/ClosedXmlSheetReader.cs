using System.Globalization;
using ClosedXML.Excel;
using PlanelyApp.Application.Abstractions.Imports;

namespace PlanelyApp.Infrastructure.Imports;

/// <summary>The only code that knows ClosedXML. Everything it returns is plain text.</summary>
internal sealed class ClosedXmlSheetReader : IImportSheetReader
{
    public Task<ImportSheet> ReadAsync(Stream content, int maxRows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRows, 1);

        try
        {
            return Task.FromResult(Read(content, maxRows, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Whatever the library throws for a file that is not a spreadsheet becomes one exception the Application knows.
            return Task.FromException<ImportSheet>(
                new ImportSheetException("The file could not be read as a spreadsheet.", exception));
        }
    }

    private static ImportSheet Read(Stream content, int maxRows, CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook(content);

        var headers = new List<string>();
        var rows = new List<ImportSheetRow>();
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
        {
            return new ImportSheet(headers, rows);
        }

        foreach (var row in worksheet.RowsUsed())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (headers.Count == 0)
            {
                var headerCells = ReadCells(row, row.LastCellUsed()?.Address.ColumnNumber ?? 0);

                if (!IsBlank(headerCells))
                {
                    headers.AddRange(headerCells);
                }

                continue;
            }

            var cells = ReadCells(row, headers.Count);

            if (IsBlank(cells))
            {
                continue;
            }

            rows.Add(new ImportSheetRow(row.RowNumber(), cells));

            // One more than the limit is enough to know the file has too many rows.
            if (rows.Count > maxRows)
            {
                break;
            }
        }

        return new ImportSheet(headers, rows);
    }

    private static string[] ReadCells(IXLRow row, int width)
    {
        var cells = new string[width];

        for (var index = 0; index < width; index++)
        {
            cells[index] = TextOf(row.Cell(index + 1));
        }

        return cells;
    }

    private static bool IsBlank(string[] cells) => cells.All(cell => cell.Length == 0);

    // A cell holds a typed value. Numbers are written the invariant way ("49.9"), whatever the language of this machine.
    private static string TextOf(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return string.Empty;
        }

        return cell.DataType switch
        {
            XLDataType.Number => cell.GetDouble().ToString("0.################", CultureInfo.InvariantCulture),
            XLDataType.Boolean => cell.GetBoolean() ? "true" : "false",
            XLDataType.DateTime => cell.GetDateTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            _ => cell.GetString().Trim(),
        };
    }
}