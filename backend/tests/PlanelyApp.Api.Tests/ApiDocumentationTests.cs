using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using PlanelyApp.Api.Documentation;
using Shouldly;

namespace PlanelyApp.Api.Tests;

public class ApiDocumentationTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_openapi_document_should_describe_the_api_in_development()
    {
        await using var app = await StartAsync(Environments.Development);

        var response = await app.GetTestClient().GetAsync("/openapi/v1.json", _ct);

        ((int)response.StatusCode).ShouldBe(200);
        var body = await TestApi.ReadAsync(response, _ct);
        body.GetProperty("info").GetProperty("title").GetString().ShouldBe("PlanelyApp API");
        body.GetProperty("paths").TryGetProperty("/ok", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task The_scalar_page_should_open_in_development()
    {
        await using var app = await StartAsync(Environments.Development);

        var response = await app.GetTestClient().GetAsync("/scalar/v1", _ct);

        ((int)response.StatusCode).ShouldBe(200);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar/v1")]
    public async Task The_documentation_should_not_exist_outside_development(string path)
    {
        await using var app = await StartAsync(Environments.Production);

        var response = await app.GetTestClient().GetAsync(path, _ct);

        ((int)response.StatusCode).ShouldBe(404);
    }

    private static Task<WebApplication> StartAsync(string environment) =>
        TestApi.StartAsync(
            api =>
            {
                api.MapApiDocumentation(api.Environment);
                api.MapGet("/ok", () => "ok");
            },
            builder => builder.Services.AddApiDocumentation(),
            environment);
}