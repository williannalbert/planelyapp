using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PlanelyApp.Infrastructure.Development;

/// <summary>
/// Runs the seeds once, in the background, when the Api starts. It must never stop the application: without the schema (the
/// migrations were not applied yet) it only warns, and the next start tries again.
/// </summary>
internal sealed class DevelopmentSeedService(
    IServiceScopeFactory scopes,
    DevelopmentSeedOptions options,
    ILogger<DevelopmentSeedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();

            var platformAdmin = scope.ServiceProvider.GetRequiredService<PlatformAdminSeed>();

            if (await platformAdmin.EnsureAsync(options, stoppingToken))
            {
                logger.LogInformation("The platform administrator of development was created.");
            }
            else
            {
                logger.LogInformation("The platform administrator of development already exists.");
            }

            if (options.Company is { } company)
            {
                var companySeed = scope.ServiceProvider.GetRequiredService<DevelopmentCompanySeed>();

                switch (await companySeed.EnsureAsync(company, stoppingToken))
                {
                    case DevelopmentCompanySeedResult.Created:
                        logger.LogInformation("The company of development and its administrator were created.");
                        break;
                    case DevelopmentCompanySeedResult.CnpjTaken:
                        logger.LogWarning(
                            "The CNPJ of the company of development belongs to another company, so its administrator was not created.");
                        break;
                    default:
                        logger.LogInformation("The company of development already exists.");
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The application is stopping.
        }
        catch (Exception exception)
        {
            // The type only: the message of a database exception may carry the connection string.
            logger.LogWarning(
                "The data of development could not be created ({ExceptionType}). Apply the migrations and start the Api again.",
                exception.GetType().Name);
        }
    }
}