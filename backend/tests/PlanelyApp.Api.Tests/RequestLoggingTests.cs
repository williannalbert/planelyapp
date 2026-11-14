using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using PlanelyApp.Api.Correlation;
using PlanelyApp.Api.Errors;
using PlanelyApp.Api.Logging;
using Shouldly;

namespace PlanelyApp.Api.Tests;

public class RequestLoggingTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly CapturingLoggerProvider _logs = new();

    [Fact]
    public async Task A_request_should_be_logged_with_its_method_path_and_status_under_the_correlation_id()
    {
        await using var app = await StartAsync();

        await SendAsync(app, "/ok", "log-1");

        var entry = RequestLog();
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Properties["Method"].ShouldBe("GET");
        entry.Properties["Path"].ShouldBe("/ok");
        entry.Properties["StatusCode"].ShouldBe(200);
        entry.Scope["CorrelationId"].ShouldBe("log-1");
    }

    [Fact]
    public async Task The_query_string_should_never_be_logged()
    {
        await using var app = await StartAsync();

        await SendAsync(app, "/ok?token=secret-value");

        var entry = RequestLog();
        entry.Message.ShouldNotContain("secret-value");
        string.Join('|', entry.Properties.Values).ShouldNotContain("secret-value");
    }

    [Theory]
    [InlineData("/ok", 200, LogLevel.Information)]
    [InlineData("/missing", 404, LogLevel.Information)]
    [InlineData("/crash", 500, LogLevel.Warning)]
    public async Task The_level_of_the_request_log_should_follow_the_status(string path, int status, LogLevel level)
    {
        await using var app = await StartAsync();

        await SendAsync(app, path);

        var entry = RequestLog();
        entry.Level.ShouldBe(level);
        entry.Properties["StatusCode"].ShouldBe(status);
    }

    [Fact]
    public async Task A_health_check_should_be_logged_only_at_debug()
    {
        await using var app = await StartAsync(LogLevel.Debug);

        await SendAsync(app, "/health/live");

        RequestLog().Level.ShouldBe(LogLevel.Debug);
    }

    [Fact]
    public async Task A_health_check_should_not_be_logged_at_the_default_level()
    {
        await using var app = await StartAsync();

        await SendAsync(app, "/health/live");

        _logs.Entries.Where(IsRequestLog).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_log_of_an_unhandled_exception_should_carry_the_correlation_id()
    {
        await using var app = await StartAsync();

        await SendAsync(app, "/crash", "crash-1");

        var entry = _logs.Entries.Single(
            candidate => candidate.Category.EndsWith(nameof(UnhandledExceptionHandler)));
        entry.Level.ShouldBe(LogLevel.Error);
        entry.Scope["CorrelationId"].ShouldBe("crash-1");
        entry.Exception.ShouldBeOfType<InvalidOperationException>();
    }

    private Task<WebApplication> StartAsync(LogLevel minimum = LogLevel.Information) =>
        TestApi.StartAsync(
            api =>
            {
                api.MapGet("/ok", () => "ok");
                api.MapGet("/crash", IResult () => throw new InvalidOperationException("boom"));
            },
            builder =>
            {
                builder.Logging.AddProvider(_logs);
                builder.Logging.SetMinimumLevel(minimum);
            });

    private async Task<HttpResponseMessage> SendAsync(WebApplication app, string path, string? correlationId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (correlationId is not null)
        {
            request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, correlationId);
        }

        return await app.GetTestClient().SendAsync(request, _ct);
    }

    private static bool IsRequestLog(CapturedLog entry) => entry.Category.EndsWith(nameof(RequestLoggingMiddleware));

    private CapturedLog RequestLog() => _logs.Entries.Single(IsRequestLog);
}