using Microsoft.Extensions.Logging;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Abstractions.Decorators;

/// <summary>
/// What is logged for every request: its name, how it ended and how long it took. Never its content,
/// which may hold personal data (LGPD).
/// </summary>
internal static class RequestLogging
{
    public static async Task<Result<TResponse>> RunAsync<TResponse>(
        ILogger logger,
        TimeProvider timeProvider,
        string request,
        Func<Task<Result<TResponse>>> run)
    {
        var started = timeProvider.GetTimestamp();
        double ElapsedMs() => timeProvider.GetElapsedTime(started).TotalMilliseconds;

        logger.LogDebug("{Request} started", request);

        try
        {
            var result = await run();

            if (result.IsSuccess)
            {
                logger.LogInformation("{Request} succeeded in {ElapsedMs:0} ms", request, ElapsedMs());
            }
            else
            {
                logger.LogWarning(
                    "{Request} failed with {ErrorType} {ErrorCode} in {ElapsedMs:0} ms",
                    request, result.Error.Type, result.Error.Code, ElapsedMs());
            }

            return result;
        }
        catch (DomainException exception)
        {
            // A rule of the business said no: expected, so a warning without the stack trace.
            logger.LogWarning(
                "{Request} refused by the domain with {ErrorCode} in {ElapsedMs:0} ms",
                request, exception.Code, ElapsedMs());
            throw;
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("{Request} was cancelled after {ElapsedMs:0} ms", request, ElapsedMs());
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "{Request} threw an exception after {ElapsedMs:0} ms", request, ElapsedMs());
            throw;
        }
    }
}