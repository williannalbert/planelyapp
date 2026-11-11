namespace PlanelyApp.IntegrationTests.Imports;

/// <summary>
/// The processor handles the pending imports of every company, so a test that runs it must not run beside the tests that
/// leave a pending import and then look at it (<c>DataImportUseCaseTests</c>). This collection runs alone.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ImportCollection
{
    public const string Name = "Imports";
}