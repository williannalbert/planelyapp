using Microsoft.Extensions.DependencyInjection;
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
}