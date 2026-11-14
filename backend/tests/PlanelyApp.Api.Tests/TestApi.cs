using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PlanelyApp.Api.Tests;

/// <summary>
/// A small Api in memory with only what 5.1a builds (errors, correlation, health) plus the endpoints a test maps.
/// A test that needs more (a log provider, the documentation) adds it in <c>configure</c>, before the Api is built.
/// The whole Api, with its database, is started by the end-to-end tests of 5.10.
/// </summary>
internal static class TestApi
{
    public static async Task<WebApplication> StartAsync(
        Action<WebApplication> map,
        Action<WebApplicationBuilder>? configure = null,
        string environment = "Production")
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddApiBasics();
        configure?.Invoke(builder);

        var app = builder.Build();
        app.UseApiBasics();
        app.MapApiBasics();
        map(app);

        await app.StartAsync();

        return app;
    }

    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return document.RootElement.Clone();
    }
}