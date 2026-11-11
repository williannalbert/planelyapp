using PlanelyApp.Application.Abstractions.Imports;

namespace PlanelyApp.Infrastructure.Imports;

/// <summary>
/// Keeps the spreadsheets in a folder of the disk, one subfolder per company. The name of the file is made here, so nothing
/// the user typed reaches the file system, and a path that leaves the folder is never opened.
/// </summary>
internal sealed class LocalImportFileStore : IImportFileStore
{
    private const int BufferSize = 81920;

    private readonly string _root;

    public LocalImportFileStore(ImportStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.RootPath));
    }

    public async Task<string> SaveAsync(Guid companyId, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("The company is required.", nameof(companyId));
        }

        var storagePath = $"{companyId:N}/{Guid.CreateVersion7():N}.xlsx";
        var fullPath = Resolve(storagePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var file = new FileStream(
            fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
        await content.CopyToAsync(file, BufferSize, cancellationToken);

        return storagePath;
    }

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken)
    {
        var fullPath = Resolve(storagePath);

        try
        {
            Stream stream = new FileStream(
                fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);

            return Task.FromResult(stream);
        }
        catch (DirectoryNotFoundException exception)
        {
            // A missing folder and a missing file are the same to the caller (and to the port).
            throw new FileNotFoundException("The file of the import is not there.", storagePath, exception);
        }
    }

    private string Resolve(string storagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);

        var fullPath = Path.GetFullPath(Path.Combine(_root, storagePath));

        // The path comes from our own database, but one that points outside the folder is never opened.
        if (!fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("The path is outside the folder of the imports.", nameof(storagePath));
        }

        return fullPath;
    }
}