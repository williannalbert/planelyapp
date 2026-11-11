using ClosedXML.Excel;

namespace PlanelyApp.IntegrationTests.Imports;

/// <summary>Builds a real .xlsx in memory, so the tests read exactly what a user would send.</summary>
internal static class XlsxBuilder
{
    public static object?[] Row(params object?[] cells) => cells;

    /// <summary>One worksheet. A null cell is left empty; numbers are stored as numbers, like Excel does.</summary>
    public static byte[] Build(params object?[][] rows)
    {
        using var workbook = new XLWorkbook();
        Fill(workbook.AddWorksheet("Sheet1"), rows);

        return Save(workbook);
    }

    public static void Fill(IXLWorksheet sheet, object?[][] rows)
    {
        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < rows[rowIndex].Length; columnIndex++)
            {
                if (rows[rowIndex][columnIndex] is not { } value)
                {
                    continue;
                }

                XLCellValue cellValue = value switch
                {
                    string text => text,
                    bool flag => flag,
                    int number => (double)number,
                    long number => (double)number,
                    decimal number => (double)number,
                    double number => number,
                    _ => throw new ArgumentException($"Type {value.GetType().Name} is not supported by the builder."),
                };

                sheet.Cell(rowIndex + 1, columnIndex + 1).Value = cellValue;
            }
        }
    }

    public static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return stream.ToArray();
    }
}