namespace PlanelyApp.Application.Imports;

/// <summary>What the Application accepts of an uploaded spreadsheet.</summary>
public static class ImportRules
{
    public const string SpreadsheetExtension = ".xlsx";

    public const int MaxFileBytes = 5 * 1024 * 1024;
}