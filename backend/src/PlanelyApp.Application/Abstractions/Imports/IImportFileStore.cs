namespace PlanelyApp.Application.Abstractions.Imports;

/// <summary>
/// Keeps the spreadsheets of the imports. A port, because where the bytes live (disk, a volume, a bucket) is the
/// Infrastructure's business. The path it returns is opaque: only the same store can open it again.
/// </summary>
public interface IImportFileStore
{
    /// <summary>Saves the content under a name of its own (never the one the user typed) and returns the path to read it back.</summary>
    Task<string> SaveAsync(Guid companyId, Stream content, CancellationToken cancellationToken);

    /// <summary>Opens the file for reading. The caller disposes the stream. Throws <see cref="FileNotFoundException"/> when it is gone.</summary>
    Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken);
}