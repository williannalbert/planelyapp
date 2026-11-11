namespace PlanelyApp.Infrastructure.Imports;

public sealed class ImportStorageOptions
{
    /// <summary>The folder where the spreadsheets are kept. In production it must be a volume that outlives the container.</summary>
    public string RootPath { get; set; } = string.Empty;

    internal void Validate() => ArgumentException.ThrowIfNullOrWhiteSpace(RootPath);
}