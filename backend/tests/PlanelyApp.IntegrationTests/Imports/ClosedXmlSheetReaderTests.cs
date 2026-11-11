using ClosedXML.Excel;
using PlanelyApp.Application.Abstractions.Imports;
using PlanelyApp.Infrastructure.Imports;
using Shouldly;
using static PlanelyApp.IntegrationTests.Imports.XlsxBuilder;

namespace PlanelyApp.IntegrationTests.Imports;

public class ClosedXmlSheetReaderTests
{
    private readonly ClosedXmlSheetReader _reader = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_first_row_should_be_the_headers_and_the_rows_keep_their_line_number()
    {
        var sheet = await ReadAsync(Build(Row("Name", "Duration"), Row("Haircut", 30), Row("Beard", 20)));

        sheet.Headers.ShouldBe(["Name", "Duration"]);
        sheet.Rows.Select(row => row.Number).ShouldBe([2, 3]);
        sheet.Rows[0].Cells.ShouldBe(["Haircut", "30"]);
        sheet.Rows[1].Cells.ShouldBe(["Beard", "20"]);
    }

    [Fact]
    public async Task Blank_rows_should_be_skipped_without_changing_the_line_numbers()
    {
        var sheet = await ReadAsync(Build(Row("Name", "Duration"), Row(null, null), Row("Haircut", 30)));

        sheet.Rows.Single().Number.ShouldBe(3);
    }

    [Fact]
    public async Task Blank_rows_above_the_headers_should_be_skipped_too()
    {
        var sheet = await ReadAsync(Build(Row(null, null), Row(null, null), Row("Name", "Duration"), Row("Haircut", 30)));

        sheet.Headers.ShouldBe(["Name", "Duration"]);
        sheet.Rows.Single().Number.ShouldBe(4);
    }

    [Fact]
    public async Task Numbers_should_be_read_the_invariant_way_without_losing_digits()
    {
        var sheet = await ReadAsync(Build(Row("Price", "Phone"), Row(49.9, 5511999990001L), Row(-5, 30)));

        sheet.Rows[0].Cells.ShouldBe(["49.9", "5511999990001"]);
        sheet.Rows[1].Cells.ShouldBe(["-5", "30"]);
    }

    [Fact]
    public async Task Booleans_and_texts_should_be_read_as_trimmed_texts()
    {
        var sheet = await ReadAsync(Build(Row("Name", "Marketing"), Row("  Ana  ", true), Row("Bruno", false)));

        sheet.Rows[0].Cells.ShouldBe(["Ana", "true"]);
        sheet.Rows[1].Cells.ShouldBe(["Bruno", "false"]);
    }

    [Fact]
    public async Task A_row_shorter_than_the_header_should_be_completed_with_empty_cells()
    {
        var sheet = await ReadAsync(Build(Row("Name", "Duration", "Price"), Row("Haircut")));

        sheet.Rows.Single().Cells.ShouldBe(["Haircut", "", ""]);
    }

    [Fact]
    public async Task Cells_beyond_the_header_should_be_ignored()
    {
        var sheet = await ReadAsync(Build(Row("Name", "Duration"), Row("Haircut", 30, "extra")));

        sheet.Rows.Single().Cells.ShouldBe(["Haircut", "30"]);
    }

    [Fact]
    public async Task It_should_stop_one_row_after_the_limit()
    {
        var rows = new List<object?[]> { Row("Name") };
        rows.AddRange(Enumerable.Range(0, 10).Select(index => Row("Service " + index)));

        var sheet = await ReadAsync(Build([.. rows]), maxRows: 3);

        sheet.Rows.Count.ShouldBe(4);
    }

    [Fact]
    public async Task A_workbook_with_no_data_should_be_an_empty_sheet()
    {
        var sheet = await ReadAsync(Build());

        sheet.Headers.ShouldBeEmpty();
        sheet.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task Only_the_first_worksheet_should_be_read()
    {
        using var workbook = new XLWorkbook();
        Fill(workbook.AddWorksheet("First"), [Row("Name"), Row("From the first")]);
        Fill(workbook.AddWorksheet("Second"), [Row("Name"), Row("From the second")]);

        var sheet = await ReadAsync(Save(workbook));

        sheet.Rows.Single().Cells.ShouldBe(["From the first"]);
    }

    [Fact]
    public async Task Bytes_that_are_not_a_spreadsheet_should_be_an_import_sheet_exception()
    {
        await Should.ThrowAsync<ImportSheetException>(() => ReadAsync([1, 2, 3, 4]));
    }

    private async Task<ImportSheet> ReadAsync(byte[] bytes, int maxRows = 100)
    {
        using var stream = new MemoryStream(bytes);

        return await _reader.ReadAsync(stream, maxRows, _ct);
    }
}