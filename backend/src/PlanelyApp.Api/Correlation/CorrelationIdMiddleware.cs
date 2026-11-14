namespace PlanelyApp.Api.Correlation;

internal sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var sent = context.Request.Headers[CorrelationId.HeaderName].ToString();
        var correlationId = CorrelationId.IsAcceptable(sent) ? sent : CorrelationId.New();

        CorrelationId.Set(context, correlationId);

        // Set when the response starts, not now: the exception handler clears the headers of a failed response.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationId.HeaderName] = correlationId;

            return Task.CompletedTask;
        });

        // Every log line of the request carries it (Serilog reads the scope in 5.1b).
        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }
}