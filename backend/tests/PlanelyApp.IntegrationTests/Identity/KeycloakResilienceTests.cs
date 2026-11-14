using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Polly.CircuitBreaker;
using Polly.Timeout;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Domain.SharedKernel;
using PlanelyApp.Infrastructure.Identity;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Identity;

/// <summary>
/// The pipeline of the Keycloak client as the container builds it (timeouts, retry, circuit breaker), over a handler
/// that plays Keycloak. No Docker: what is under test is how the client behaves when Keycloak is slow or down.
/// </summary>
public class KeycloakResilienceTests
{
    private const string UsersPath = "/admin/realms/planelyapp/users";
    private const string AccountPath = UsersPath + "/abc-123";
    private const string StoredAccount = """{"id":"abc-123","username":"a@example.com","email":"a@example.com","enabled":true}""";

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static IdentityProfile Profile() => new("Bia Lopes", Email.Create("bia@example.com"), null);

    private static ServiceProvider Build(ScriptedHandler handler, Action<KeycloakOptions>? configure = null)
    {
        var options = new KeycloakOptions
        {
            BaseUrl = "http://keycloak.test",
            Realm = "planelyapp",
            ClientId = "planelyapp-api",
            ClientSecret = "secret",
            SendSetupEmail = false,
            RequestTimeout = TimeSpan.FromSeconds(10),
            AttemptTimeout = TimeSpan.FromSeconds(5),
        };
        configure?.Invoke(options);

        var services = new ServiceCollection();
        services.AddKeycloakIdentityProvider(options);

        // Registered after the real one, so it replaces only what answers: the resilience stays.
        services.AddHttpClient(IdentityServiceCollectionExtensions.KeycloakClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task A_read_that_got_a_503_should_be_tried_again()
    {
        var handler = new ScriptedHandler((key, call, _) => Task.FromResult(key switch
        {
            "GET " + AccountPath => call == 1
                ? Reply.Status(HttpStatusCode.ServiceUnavailable)()
                : Reply.Json(HttpStatusCode.OK, StoredAccount)(),
            "PUT " + AccountPath => Reply.Status(HttpStatusCode.NoContent)(),
            _ => throw new InvalidOperationException($"No answer for {key}."),
        }));
        await using var services = Build(handler);

        await services.GetRequiredService<IIdentityProvider>().SetEnabledAsync("abc-123", false, _ct);

        handler.Calls("GET", AccountPath).ShouldBe(2);
        handler.Calls("PUT", AccountPath).ShouldBe(1);
    }

    [Fact]
    public async Task A_read_that_cannot_reach_keycloak_should_be_tried_again_and_then_fail_as_an_identity_error()
    {
        var handler = new ScriptedHandler((_, _, _) => throw new HttpRequestException("connection refused"));
        await using var services = Build(handler);

        var failure = await Should.ThrowAsync<IdentityProviderException>(
            () => services.GetRequiredService<IIdentityProvider>().SetEnabledAsync("abc-123", false, _ct));

        handler.Calls("GET", AccountPath).ShouldBe(3);
        failure.InnerException.ShouldBeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task Creating_an_account_should_never_be_repeated()
    {
        // A repeat could create two accounts: the answer is lost, not necessarily the work.
        var handler = new ScriptedHandler((key, _, _) => Task.FromResult(key switch
        {
            "POST " + UsersPath => Reply.Status(HttpStatusCode.ServiceUnavailable)(),
            _ => throw new InvalidOperationException($"No answer for {key}."),
        }));
        await using var services = Build(handler);

        await Should.ThrowAsync<IdentityProviderException>(
            () => services.GetRequiredService<IIdentityProvider>().CreateUserAsync(Profile(), _ct));

        handler.Calls("POST", UsersPath).ShouldBe(1);
    }

    [Fact]
    public async Task A_conflict_should_not_count_against_the_circuit()
    {
        var handler = new ScriptedHandler((key, _, _) => Task.FromResult(key switch
        {
            "POST " + UsersPath => Reply.Status(HttpStatusCode.Conflict)(),
            _ => throw new InvalidOperationException($"No answer for {key}."),
        }));
        await using var services = Build(handler);
        var provider = services.GetRequiredService<IIdentityProvider>();

        // More than the minimum throughput, all refused by Keycloak and none a failure of Keycloak.
        for (var i = 0; i < 8; i++)
        {
            var result = await provider.CreateUserAsync(Profile(), _ct);

            result.IsSuccess.ShouldBeFalse();
            result.Error.Code.ShouldBe("Identity.EmailAlreadyInUse");
        }

        handler.Calls("POST", UsersPath).ShouldBe(8);
    }

    [Fact]
    public async Task A_keycloak_that_keeps_failing_should_open_the_circuit_and_fail_fast()
    {
        var handler = new ScriptedHandler((key, _, _) => Task.FromResult(key switch
        {
            "DELETE " + AccountPath => Reply.Status(HttpStatusCode.InternalServerError)(),
            _ => throw new InvalidOperationException($"No answer for {key}."),
        }));
        await using var services = Build(handler);
        var provider = services.GetRequiredService<IIdentityProvider>();
        IdentityProviderException? last = null;

        for (var i = 0; i < 12; i++)
        {
            last = await Should.ThrowAsync<IdentityProviderException>(() => provider.DeleteUserAsync("abc-123", _ct));
        }

        // Once it opened, the calls no longer reach Keycloak.
        handler.Calls("DELETE", AccountPath).ShouldBeLessThan(12);
        last!.InnerException.ShouldBeOfType<BrokenCircuitException>();
    }

    [Fact]
    public async Task A_keycloak_that_is_too_slow_should_be_cut_at_the_attempt_timeout()
    {
        var handler = new ScriptedHandler(async (_, _, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);

            return Reply.Status(HttpStatusCode.OK)();
        });
        await using var services = Build(handler, options =>
        {
            options.RequestTimeout = TimeSpan.FromSeconds(2);
            options.AttemptTimeout = TimeSpan.FromMilliseconds(300);
        });

        var failure = await Should.ThrowAsync<IdentityProviderException>(
            () => services.GetRequiredService<IIdentityProvider>().DeleteUserAsync("abc-123", _ct));

        failure.InnerException.ShouldBeOfType<TimeoutRejectedException>();
    }
}

/// <summary>Plays Keycloak: answers the token route by itself and calls the test for everything else, counting the calls.</summary>
internal sealed class ScriptedHandler(Func<string, int, CancellationToken, Task<HttpResponseMessage>> answer) : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, int> _calls = new();

    public int Calls(string method, string path) => _calls.GetValueOrDefault($"{method} {path}");

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;

        if (path.EndsWith("/protocol/openid-connect/token", StringComparison.Ordinal))
        {
            return Task.FromResult(Reply.Token("token")());
        }

        var key = $"{request.Method} {path}";
        var call = _calls.AddOrUpdate(key, 1, (_, count) => count + 1);

        return answer(key, call, cancellationToken);
    }
}
