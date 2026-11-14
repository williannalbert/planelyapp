using Serilog;
using Serilog.Formatting.Compact;

namespace PlanelyApp.Api.Logging;

internal static class ApiLoggingExtensions
{
    private const string DevelopmentTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Serilog as the provider of <see cref="ILogger"/>. The levels come from the <c>Serilog</c> section; the format
    /// comes from the environment. <paramref name="customize"/> lets a test add a sink.
    /// </summary>
    public static IServiceCollection AddApiLogging(
        this IServiceCollection services,
        IHostEnvironment environment,
        Action<LoggerConfiguration>? customize = null) =>
        services.AddSerilog((serviceProvider, logger) =>
        {
            logger
                .ReadFrom.Configuration(serviceProvider.GetRequiredService<IConfiguration>())
                .Enrich.FromLogContext();

            if (environment.IsDevelopment())
            {
                logger.WriteTo.Console(outputTemplate: DevelopmentTemplate);
            }
            else
            {
                logger.WriteTo.Console(new CompactJsonFormatter());
            }

            customize?.Invoke(logger);
        });
}