using System.Collections.Concurrent;
using PlanelyApp.Application.Abstractions.Imports;

namespace PlanelyApp.IntegrationTests;

/// <summary>Keeps the spreadsheets in memory, so a test can look at what was stored without touching the disk.</summary>
public sealed class FakeImportFileStore : IImportFileStore
{
    private readonly ConcurrentDictionary<string, (Guid CompanyId, byte[] Content)> _files = new();

    public async Task<string> SaveAsync(Guid companyId, Stream content, CancellationToken cancellationToken)
    {
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);

        var path = $"{companyId:N}/{Guid.NewGuid():N}.xlsx";
        _files[path] = (companyId, copy.ToArray());

        return path;
    }

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken) =>
        _files.TryGetValue(storagePath, out var file)
            ? Task.FromResult<Stream>(new MemoryStream(file.Content, writable: false))
            : throw new FileNotFoundException("The file of the import is not there (test).", storagePath);

    public byte[] ContentOf(string storagePath) => _files[storagePath].Content;

    public int CountFor(Guid companyId) => _files.Values.Count(file => file.CompanyId == companyId);
}