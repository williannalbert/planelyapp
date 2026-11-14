using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Api.Tests;

/// <summary>What the Api answers when the database or Keycloak fails in a way the caller can only retry later.</summary>
public class ExternalFailureTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_lost_race_should_be_a_409_that_does_not_leak_the_inner_message()
    {
        await using var app = await TestApi.StartAsync(api => api.MapPut(
            "/race",
            IResult () => throw new ConcurrencyConflictException(new InvalidOperationException("secret detail"))));

        var response = await app.GetTestClient().PutAsync("/race", null, _ct);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var text = await response.Content.ReadAsStringAsync(_ct);
        text.ShouldNotContain("secret detail");
        text.ShouldNotContain("nothing was saved");
        using var body = System.Text.Json.JsonDocument.Parse(text);
        body.RootElement.GetProperty("code").GetString().ShouldBe("Concurrency.Conflict");
        body.RootElement.GetProperty("correlationId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task An_identity_provider_that_cannot_be_reached_should_be_a_503_that_does_not_leak_its_message()
    {
        await using var app = await TestApi.StartAsync(api => api.MapPost(
            "/users",
            IResult () => throw new IdentityProviderException("Keycloak said: secret detail")));

        var response = await app.GetTestClient().PostAsync("/users", null, _ct);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var text = await response.Content.ReadAsStringAsync(_ct);
        text.ShouldNotContain("secret detail");
        using var body = System.Text.Json.JsonDocument.Parse(text);
        body.RootElement.GetProperty("code").GetString().ShouldBe("Identity.Unavailable");
        body.RootElement.GetProperty("correlationId").GetString().ShouldNotBeNullOrWhiteSpace();
    }
}
