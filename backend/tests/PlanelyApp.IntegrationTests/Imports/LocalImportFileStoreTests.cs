using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Application.Abstractions.Imports;
using PlanelyApp.Infrastructure.Imports;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Imports;

public sealed class LocalImportFileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "planelyapp-import-tests", Guid.NewGuid().ToString("N"));
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly LocalImportFileStore _store;

    public LocalImportFileStoreTests() => _store = new LocalImportFileStore(new ImportStorageOptions { RootPath = _root });

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task A_saved_file_should_be_read_back_as_it_was_sent()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        using var content = new MemoryStream(bytes);

        var path = await _store.SaveAsync(Guid.CreateVersion7(), content, _ct);

        await using var stream = await _store.OpenReadAsync(path, _ct);
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy, _ct);
        copy.ToArray().ShouldBe(bytes);
    }

    [Fact]
    public async Task The_path_should_be_made_by_the_store_inside_the_folder_of_the_company()
    {
        var company = Guid.CreateVersion7();
        using var content = new MemoryStream(new byte[] { 1 });

        var path = await _store.SaveAsync(company, content, _ct);

        path.ShouldStartWith($"{company:N}/");
        path.ShouldEndWith(".xlsx");
        path.ShouldNotContain(_root);
    }

    [Fact]
    public async Task Two_saves_should_never_share_a_path()
    {
        var company = Guid.CreateVersion7();
        using var first = new MemoryStream(new byte[] { 1 });
        using var second = new MemoryStream(new byte[] { 2 });

        var one = await _store.SaveAsync(company, first, _ct);
        var other = await _store.SaveAsync(company, second, _ct);

        one.ShouldNotBe(other);
    }

    [Theory]
    [InlineData("../outside.xlsx")]
    [InlineData("a/../../outside.xlsx")]
    public async Task A_path_that_leaves_the_folder_should_be_refused(string path) =>
        await Should.ThrowAsync<ArgumentException>(async () => await _store.OpenReadAsync(path, _ct));

    [Fact]
    public async Task An_absolute_path_should_be_refused()
    {
        var absolute = Path.Combine(Path.GetTempPath(), "outside.xlsx");

        await Should.ThrowAsync<ArgumentException>(async () => await _store.OpenReadAsync(absolute, _ct));
    }

    [Fact]
    public async Task A_missing_file_in_a_known_folder_should_be_file_not_found()
    {
        var company = Guid.CreateVersion7();
        using var content = new MemoryStream(new byte[] { 1 });
        await _store.SaveAsync(company, content, _ct);

        await Should.ThrowAsync<FileNotFoundException>(
            async () => await _store.OpenReadAsync($"{company:N}/{Guid.NewGuid():N}.xlsx", _ct));
    }

    [Fact]
    public async Task A_missing_folder_should_also_be_file_not_found()
    {
        await Should.ThrowAsync<FileNotFoundException>(
            async () => await _store.OpenReadAsync($"{Guid.NewGuid():N}/file.xlsx", _ct));
    }

    [Fact]
    public void An_empty_root_should_be_refused()
    {
        Should.Throw<ArgumentException>(() => new LocalImportFileStore(new ImportStorageOptions()));
    }

    [Fact]
    public void The_registration_should_give_the_store()
    {
        using var provider = new ServiceCollection()
            .AddImportFileStorage(new ImportStorageOptions { RootPath = _root })
            .BuildServiceProvider();

        provider.GetRequiredService<IImportFileStore>().ShouldNotBeNull();
    }
}