using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using PlanelyApp.Api.Correlation;
using PlanelyApp.Api.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Shouldly;

namespace PlanelyApp.Api.Tests;

public class ApiLoggingTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly CollectingSink _sink = new();

    [Fact]
    public async Task The_correlation_id_of_the_request_should_be_a_property_of_the_serilog_events()
    {
        await using var app = await StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/ok");
        request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, "serilog-1");

        await app.GetTestClient().SendAsync(request, _ct);

        var events = _sink.Events.Where(logEvent => logEvent.Properties.ContainsKey("CorrelationId")).ToList();
        events.ShouldNotBeEmpty();
        events.Any(logEvent => logEvent.MessageTemplate.Text.Contains("responded")).ShouldBeTrue();
        events
            .Select(logEvent => ((ScalarValue)logEvent.Properties["CorrelationId"]).Value)
            .Distinct()
            .ShouldBe(new object?[] { "serilog-1" });
    }

    [Fact]
    public async Task The_minimum_level_should_come_from_the_configuration()
    {
        await using var app = await StartAsync(builder => builder.Configuration["Serilog:MinimumLevel:Default"] = "Warning");

        await app.GetTestClient().GetAsync("/ok", _ct);

        _sink.Events.Where(logEvent => logEvent.Level < LogEventLevel.Warning).ShouldBeEmpty();
    }

    private Task<WebApplication> StartAsync(Action<WebApplicationBuilder>? configure = null) =>
        TestApi.StartAsync(
            api => api.MapGet("/ok", () => "ok"),
            builder =>
            {
                configure?.Invoke(builder);
                builder.Services.AddApiLogging(builder.Environment, logger => logger.WriteTo.Sink(_sink));
            });

    private sealed class CollectingSink : ILogEventSink
    {
        private readonly ConcurrentQueue<LogEvent> _events = new();

        public IReadOnlyList<LogEvent> Events => [.. _events];

        public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);
    }
}