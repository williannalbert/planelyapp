using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Api.Authentication;
using PlanelyApp.Api.Endpoints.Account;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Users.ResolveActor;
using PlanelyApp.Domain.Organization.Users;
using Shouldly;

namespace PlanelyApp.Api.Tests;

/// <summary>
/// The staff signs in at Keycloak and sends the token. The Api trusts a token only for the right realm, audience, key and
/// time, then finds the user of the token in its own database; the role and the company come from there, never from the token.
/// </summary>
public class AuthenticationTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly Guid CompanyId = Guid.CreateVersion7();

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static FakeDispatcher DispatcherWithTheUser(UserRole role = UserRole.CompanyAdmin, Guid? companyId = null) =>
        new() { Resolve = _ => new StaffActor(UserId, companyId ?? CompanyId, role, null) };

    private static Task<WebApplication> StartAsync(FakeDispatcher dispatcher) => TestApi.StartAsync(
        api =>
        {
            api.UseApiAuthentication();
            api.MapMe();
            api.MapGet("/closed", () => "closed");
            api.MapGet("/open", () => "open").AllowAnonymous();
            api.MapGet("/same-instance", (ICurrentUser asInterface, CurrentUser asClass) => ReferenceEquals(asInterface, asClass));
        },
        builder =>
        {
            builder.Services.AddSingleton<IDispatcher>(dispatcher);
            builder.Services.AddApiAuthentication(TestIdentity.Keycloak, builder.Environment);

            // The key of the test instead of the metadata of a Keycloak that does not exist here.
            builder.Services.Configure<JwtBearerOptions>(
                ApiAuthenticationExtensions.StaffScheme, options => options.ConfigurationManager = TestIdentity.Metadata);
        });

    private async Task<HttpResponseMessage> GetAsync(
        WebApplication app,
        string path,
        string? token = null,
        string? language = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (language is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", language);
        }

        return await app.GetTestClient().SendAsync(request, _ct);
    }

    [Fact]
    public async Task A_valid_token_of_a_registered_user_should_open_the_door_and_say_who_is_acting()
    {
        var dispatcher = DispatcherWithTheUser(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await GetAsync(app, "/me", TestIdentity.Token());
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.GetProperty("id").GetGuid().ShouldBe(UserId);
        body.GetProperty("kind").GetString().ShouldBe("Staff");
        body.GetProperty("role").GetString().ShouldBe("Receptionist");
        body.GetProperty("companyId").GetGuid().ShouldBe(CompanyId);
    }

    [Fact]
    public async Task The_platform_administrator_should_have_no_company()
    {
        var dispatcher = new FakeDispatcher { Resolve = _ => new StaffActor(UserId, null, UserRole.PlatformAdmin, []) };
        await using var app = await StartAsync(dispatcher);

        var response = await GetAsync(app, "/me", TestIdentity.Token());
        var body = await TestApi.ReadAsync(response, _ct);

        body.GetProperty("role").GetString().ShouldBe("PlatformAdmin");
        body.GetProperty("companyId").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task The_user_should_be_looked_up_by_the_subject_of_the_token_once_per_request()
    {
        var dispatcher = DispatcherWithTheUser();
        await using var app = await StartAsync(dispatcher);

        await GetAsync(app, "/me", TestIdentity.Token(subject: "kc-someone-else"));

        dispatcher.Asked.ShouldBe(["kc-someone-else"]);
    }

    [Fact]
    public async Task The_current_user_of_the_use_cases_should_be_the_one_the_middleware_filled()
    {
        await using var app = await StartAsync(DispatcherWithTheUser());

        var response = await GetAsync(app, "/same-instance", TestIdentity.Token());

        (await response.Content.ReadAsStringAsync(_ct)).ShouldBe("true");
    }

    [Fact]
    public async Task A_request_with_no_token_should_be_a_401_problem_that_asks_for_a_bearer_token()
    {
        await using var app = await StartAsync(DispatcherWithTheUser());

        var response = await GetAsync(app, "/closed");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        body.GetProperty("code").GetString().ShouldBe("Http.Unauthorized");
        body.GetProperty("detail").GetString().ShouldBe("É preciso entrar para acessar este recurso.");
        response.Headers.WwwAuthenticate.ToString().ShouldStartWith("Bearer");
    }

    [Fact]
    public async Task The_401_should_speak_the_language_of_the_request()
    {
        await using var app = await StartAsync(DispatcherWithTheUser());

        var response = await GetAsync(app, "/closed", language: "en");
        var body = await TestApi.ReadAsync(response, _ct);

        body.GetProperty("detail").GetString().ShouldBe("You must sign in to access this resource.");
    }

    // The token is made inside the test: a token in the data would change every run, and so would the id of the test.
    private static string BrokenToken(string kind) => kind switch
    {
        "expired" => TestIdentity.Token(lifetime: TimeSpan.FromMinutes(-10)),
        "from another realm" => TestIdentity.Token(issuer: "http://keycloak.test/realms/other"),
        "for another audience" => TestIdentity.Token(audience: "account"),
        "with no audience" => TestIdentity.Token(audience: null),
        "signed by another key" => TestIdentity.Token(credentials: TestIdentity.StrangerKey),
        "signed with a shared secret" => TestIdentity.Token(credentials: TestIdentity.SharedSecret),
        "not a token" => "this-is-not-a-jwt",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    [Theory]
    [InlineData("expired")]
    [InlineData("from another realm")]
    [InlineData("for another audience")]
    [InlineData("with no audience")]
    [InlineData("signed by another key")]
    [InlineData("signed with a shared secret")]
    [InlineData("not a token")]
    public async Task A_token_that_cannot_be_trusted_should_be_a_401_and_the_database_should_not_be_asked(string kind)
    {
        var dispatcher = DispatcherWithTheUser();
        await using var app = await StartAsync(dispatcher);

        var response = await GetAsync(app, "/closed", BrokenToken(kind));
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, kind);
        body.GetProperty("code").GetString().ShouldBe("Http.Unauthorized");
        dispatcher.Asked.ShouldBeEmpty(kind);
    }

    [Fact]
    public async Task A_valid_token_with_no_subject_should_be_a_401()
    {
        var dispatcher = DispatcherWithTheUser();
        await using var app = await StartAsync(dispatcher);

        var response = await GetAsync(app, "/closed", TestIdentity.Token(subject: null));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        dispatcher.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_valid_token_of_someone_who_is_not_a_user_should_be_a_403_with_its_own_code()
    {
        await using var app = await StartAsync(new FakeDispatcher());

        var response = await GetAsync(app, "/closed", TestIdentity.Token());
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        body.GetProperty("code").GetString().ShouldBe("User.NotRegistered");
        body.GetProperty("detail").GetString().ShouldBe("Sua conta não está cadastrada no PlanelyApp.");
    }

    [Fact]
    public async Task A_deactivated_user_should_be_a_403_in_the_language_of_the_request()
    {
        var dispatcher = new FakeDispatcher { Resolve = _ => Error.Forbidden("User.Inactive") };
        await using var app = await StartAsync(dispatcher);

        var response = await GetAsync(app, "/closed", TestIdentity.Token(), language: "es");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        body.GetProperty("code").GetString().ShouldBe("User.Inactive");
        body.GetProperty("detail").GetString().ShouldBe("Tu usuario está desactivado.");
    }

    [Fact]
    public async Task An_open_endpoint_should_answer_with_no_token()
    {
        var dispatcher = DispatcherWithTheUser();
        await using var app = await StartAsync(dispatcher);

        var response = await GetAsync(app, "/open");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        dispatcher.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_open_endpoint_should_not_look_the_user_up_even_when_a_token_comes()
    {
        var dispatcher = new FakeDispatcher();
        await using var app = await StartAsync(dispatcher);

        var response = await GetAsync(app, "/open", TestIdentity.Token());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        dispatcher.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_liveness_check_should_answer_with_no_token()
    {
        await using var app = await StartAsync(DispatcherWithTheUser());

        var response = await GetAsync(app, "/health/live");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_unknown_route_should_be_a_401_without_a_token_so_that_the_routes_are_not_revealed()
    {
        var dispatcher = DispatcherWithTheUser();
        await using var app = await StartAsync(dispatcher);

        var response = await GetAsync(app, "/there-is-no-such-route");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        body.GetProperty("code").GetString().ShouldBe("Http.Unauthorized");
        dispatcher.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_unknown_route_should_be_a_404_for_someone_who_is_signed_in_and_not_ask_for_the_user()
    {
        var dispatcher = DispatcherWithTheUser();
        await using var app = await StartAsync(dispatcher);

        var response = await GetAsync(app, "/there-is-no-such-route", TestIdentity.Token());
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.GetProperty("code").GetString().ShouldBe("Http.NotFound");
        dispatcher.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_token_should_never_reach_the_body_of_an_error()
    {
        await using var app = await StartAsync(DispatcherWithTheUser());
        var token = TestIdentity.Token(credentials: TestIdentity.StrangerKey);

        var response = await GetAsync(app, "/closed", token);

        (await response.Content.ReadAsStringAsync(_ct)).ShouldNotContain(token);
    }
}
