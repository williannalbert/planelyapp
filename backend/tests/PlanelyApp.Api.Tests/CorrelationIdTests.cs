using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using PlanelyApp.Api.Correlation;
using Shouldly;

namespace PlanelyApp.Api.Tests;

public class CorrelationIdTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_id_the_caller_sent_should_come_back()
    {
        await using var app = await TestApi.StartAsync(api => api.MapGet("/ok", () => "ok"));

        var response = await SendAsync(app, "/ok", "front-123.abc_X");

        response.Headers.GetValues(CorrelationId.HeaderName).Single().ShouldBe("front-123.abc_X");
    }

    [Theory]
    [InlineData("has spaces")]
    [InlineData("a,b")]
    [InlineData("<script>")]
    public async Task An_id_with_unsafe_characters_should_be_replaced_by_a_new_one(string sent)
    {
        await using var app = await TestApi.StartAsync(api => api.MapGet("/ok", () => "ok"));

        var response = await SendAsync(app, "/ok", sent);

        var received = response.Headers.GetValues(CorrelationId.HeaderName).Single();
        received.ShouldNotBe(sent);
        Guid.TryParseExact(received, "N", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task A_request_without_an_id_should_get_one()
    {
        await using var app = await TestApi.StartAsync(api => api.MapGet("/ok", () => "ok"));

        var response = await SendAsync(app, "/ok", null);

        Guid.TryParseExact(response.Headers.GetValues(CorrelationId.HeaderName).Single(), "N", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task A_failed_response_should_carry_the_id_in_the_header_and_in_the_body()
    {
        await using var app = await TestApi.StartAsync(api =>
            api.MapGet("/crash", IResult () => throw new InvalidOperationException("boom")));

        var response = await SendAsync(app, "/crash", "trace-me-1");

        ((int)response.StatusCode).ShouldBe(500);
        response.Headers.GetValues(CorrelationId.HeaderName).Single().ShouldBe("trace-me-1");
        var body = await TestApi.ReadAsync(response, _ct);
        body.GetProperty("correlationId").GetString().ShouldBe("trace-me-1");
    }

    private async Task<HttpResponseMessage> SendAsync(WebApplication app, string path, string? correlationId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (correlationId is not null)
        {
            request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, correlationId);
        }

        return await app.GetTestClient().SendAsync(request, _ct);
    }
}