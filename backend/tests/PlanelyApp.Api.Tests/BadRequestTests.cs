using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Shouldly;

namespace PlanelyApp.Api.Tests;

/// <summary>
/// A body the framework cannot read (broken JSON, a field of the wrong type, an empty body, a content type that is not JSON) is a
/// 400 or a 415 with a code and a text, the same in Development and in Production, and the text of the framework never
/// reaches the client.
/// </summary>
public class BadRequestTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static Task<WebApplication> StartAsync(string environment, List<string> reached) => TestApi.StartAsync(
        api =>
        {
            api.MapPost("/echo", (EchoBody body) =>
            {
                reached.Add("echo");

                return Results.Ok(body);
            });
            api.MapGet("/crash", IResult () => throw new InvalidOperationException("secret detail"));
        },
        environment: environment);

    private async Task<HttpResponseMessage> PostAsync(
        WebApplication app, string body, string contentType = "application/json", string? language = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/echo")
        {
            Content = new StringContent(body, Encoding.UTF8, contentType),
        };

        if (language is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", language);
        }

        return await app.GetTestClient().SendAsync(request, _ct);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public async Task A_body_that_is_not_valid_json_should_be_a_400_problem(string environment)
    {
        var reached = new List<string>();
        await using var app = await StartAsync(environment, reached);

        var response = await PostAsync(app, "{ \"name\": ");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        body.GetProperty("code").GetString().ShouldBe("Http.BadRequest");
        body.GetProperty("detail").GetString().ShouldNotBeNullOrWhiteSpace();
        body.GetProperty("correlationId").GetString().ShouldNotBeNullOrWhiteSpace();
        reached.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public async Task A_field_of_the_wrong_type_should_be_a_400_problem(string environment)
    {
        var reached = new List<string>();
        await using var app = await StartAsync(environment, reached);

        var response = await PostAsync(app, "{ \"name\": \"Ana\", \"count\": \"many\" }");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("code").GetString().ShouldBe("Http.BadRequest");
        reached.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public async Task An_empty_body_should_be_a_400_problem(string environment)
    {
        var reached = new List<string>();
        await using var app = await StartAsync(environment, reached);

        var response = await PostAsync(app, string.Empty);
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("code").GetString().ShouldBe("Http.BadRequest");
        reached.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public async Task A_content_type_that_is_not_json_should_be_a_415_problem(string environment)
    {
        var reached = new List<string>();
        await using var app = await StartAsync(environment, reached);

        var response = await PostAsync(app, "name=Ana", "text/plain");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
        body.GetProperty("code").GetString().ShouldBe("Http.UnsupportedMediaType");
        body.GetProperty("detail").GetString().ShouldNotBeNullOrWhiteSpace();
        reached.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public async Task The_text_of_the_framework_should_never_reach_the_client(string environment)
    {
        await using var app = await StartAsync(environment, []);

        var response = await PostAsync(app, "{ \"name\": ");
        var text = await response.Content.ReadAsStringAsync(_ct);

        text.ShouldNotContain("Failed to read");
        text.ShouldNotContain("System.Text.Json");
        text.ShouldNotContain("Microsoft.AspNetCore");
        text.ShouldNotContain("EchoBody");
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public async Task The_answer_should_speak_the_language_of_the_request(string environment)
    {
        await using var app = await StartAsync(environment, []);

        var response = await PostAsync(app, "{ \"name\": ", language: "es");
        var body = await TestApi.ReadAsync(response, _ct);

        response.Content.Headers.ContentLanguage.ShouldContain("es");
        body.GetProperty("code").GetString().ShouldBe("Http.BadRequest");
    }

    [Fact]
    public async Task A_valid_body_should_still_reach_the_endpoint()
    {
        var reached = new List<string>();
        await using var app = await StartAsync("Development", reached);

        var response = await PostAsync(app, "{ \"name\": \"Ana\", \"count\": 2 }");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        reached.ShouldBe(["echo"]);
    }

    [Fact]
    public async Task Any_other_failure_should_stay_a_500_in_development_too()
    {
        await using var app = await StartAsync("Development", []);

        var response = await app.GetTestClient().GetAsync("/crash", _ct);
        var text = await response.Content.ReadAsStringAsync(_ct);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        text.ShouldContain("Server.UnexpectedError");
        text.ShouldNotContain("secret detail");
    }

    private sealed record EchoBody(string Name, int Count);
}
