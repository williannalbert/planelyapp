using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PlanelyApp.Application.Abstractions.Imports;

namespace PlanelyApp.Infrastructure.Imports;

public static class ImportServiceCollectionExtensions
{
    /// <summary>The store of the spreadsheets, in a folder of the disk. The options also tell where in production (5.9).</summary>
    public static IServiceCollection AddImportFileStorage(this IServiceCollection services, ImportStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton<IImportFileStore>(new LocalImportFileStore(options));

        return services;
    }

    /// <summary>The reader and the processor of the imports, without a worker. Tests and one-off runs use this.</summary>
    public static IServiceCollection AddImportProcessing(this IServiceCollection services, Action<ImportOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new ImportOptions();
        configure?.Invoke(options);
        options.Validate();

        services.AddLogging();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(options);
        services.AddSingleton<IImportSheetReader, ClosedXmlSheetReader>();
        services.AddSingleton<ImportProcessor>();

        return services;
    }

    /// <summary>The processing plus the background service that looks for imports every <see cref="ImportOptions.PollingInterval"/>.</summary>
    public static IServiceCollection AddImportWorker(this IServiceCollection services, Action<ImportOptions>? configure = null)
    {
        services.AddImportProcessing(configure);
        services.AddHostedService<ImportWorkerService>();

        return services;
    }
}