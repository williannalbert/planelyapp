using System.Diagnostics;

namespace PlanelyApp.Api.Logging;

/// <summary>
/// One line per request, written when its response ends: method, path, status and time. Never the query string, the
/// headers or the body, which can hold personal data. It runs inside the correlation id middleware, so the line has the id.
/// </summary>
internal sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    private const string HealthPath = "/health";

    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();

        // Stays 500 when something escapes the exception handler: the caller got a failure of the server.
        var statusCode = StatusCodes.Status500InternalServerError;

        try
        {
            await next(context);
            statusCode = context.Response.StatusCode;
        }
        finally
        {
            logger.Log(
                LevelOf(context, statusCode),
                "{Method} {Path} responded {StatusCode} in {ElapsedMs:0.0} ms",
                context.Request.Method,
                context.Request.Path.Value,
                statusCode,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private static LogLevel LevelOf(HttpContext context, int statusCode)
    {
        // A probe every few seconds would bury everything else.
        if (context.Request.Path.StartsWithSegments(HealthPath))
        {
            return LogLevel.Debug;
        }

        return statusCode >= StatusCodes.Status500InternalServerError ? LogLevel.Warning : LogLevel.Information;
    }
}