namespace PlanelyApp.Application.Imports.Processing;

/// <summary>Where each known column is in the file.</summary>
internal sealed class ImportColumnMap(IReadOnlyDictionary<string, int> columns)
{
    public bool Has(string key) => columns.ContainsKey(key);

    /// <summary>The trimmed text of the cell, or an empty text when the column or the cell is not there.</summary>
    public string Get(IReadOnlyList<string> cells, string key) =>
        columns.TryGetValue(key, out var index) && index < cells.Count ? cells[index].Trim() : string.Empty;
}