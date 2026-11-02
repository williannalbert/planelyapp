using System.Net;
using System.Text.Json.Nodes;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.SharedKernel;
using PlanelyApp.Infrastructure.Identity;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Identity;

public class KeycloakIdentityProviderStubTests
{
    private const string TokenPath = "/realms/planelyapp/protocol/openid-connect/token";
    private const string UsersPath = "/admin/realms/planelyapp/users";
    private const string AccountPath = UsersPath + "/abc-123";
    private const string StoredAccount = """
        {"id":"abc-123","username":"old@example.com","email":"old@example.com","firstName":"Old","lastName":"Name",
         "enabled":true,"emailVerified":true,"attributes":{"locale":["en"],"phone":["+5511"]}}
        """;

    private readonly StubKeycloak _stub = new();
    private readonly ManualTimeProvider _clock = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public KeycloakIdentityProviderStubTests()
    {
        _stub.On(HttpMethod.Post, TokenPath, Reply.Token("t1"));
    }

    private static IdentityProfile Profile(Language? language = null) =>
        new("Bia Lopes", Email.Create("bia@example.com"), language);

    private KeycloakIdentityProvider Provider(Action<KeycloakOptions>? configure = null)
    {
        var options = new KeycloakOptions
        {
            BaseUrl = "http://keycloak.test",
            Realm = "planelyapp",
            ClientId = "planelyapp-api",
            ClientSecret = "secret",
        };
        configure?.Invoke(options);
        var http = new HttpClient(_stub) { BaseAddress = new Uri("http://keycloak.test/") };

        return new KeycloakIdentityProvider(http, options, new KeycloakTokenProvider(http, options, _clock));
    }

    private RecordedRequest Single(HttpMethod method, string path) =>
        _stub.Requests.Single(request => request.Method == method && request.Path == path);

    private static JsonNode BodyOf(RecordedRequest request) => JsonNode.Parse(request.Body)!;

    [Fact]
    public async Task The_token_should_be_requested_with_the_service_client_and_reused()
    {
        _stub.On(HttpMethod.Delete, AccountPath, Reply.Status(HttpStatusCode.NoContent));
        var provider = Provider();

        await provider.DeleteUserAsync("abc-123", _ct);
        await provider.DeleteUserAsync("abc-123", _ct);

        var tokenRequest = Single(HttpMethod.Post, TokenPath);
        tokenRequest.Body.ShouldContain("grant_type=client_credentials");
        tokenRequest.Body.ShouldContain("client_id=planelyapp-api");
        tokenRequest.Body.ShouldContain("client_secret=secret");
        _stub.Requests.Where(request => request.Method == HttpMethod.Delete)
            .Select(request => request.Token).ShouldBe(["t1", "t1"], ignoreOrder: false);
    }

    [Fact]
    public async Task An_expired_token_should_be_renewed()
    {
        _stub.On(HttpMethod.Post, TokenPath, Reply.Token("t1", 60), Reply.Token("t2"));
        _stub.On(HttpMethod.Delete, AccountPath, Reply.Status(HttpStatusCode.NoContent));
        var provider = Provider();

        await provider.DeleteUserAsync("abc-123", _ct);
        _clock.Advance(TimeSpan.FromSeconds(31));
        await provider.DeleteUserAsync("abc-123", _ct);

        _stub.Requests.Where(request => request.Method == HttpMethod.Delete)
            .Select(request => request.Token).ShouldBe(["t1", "t2"], ignoreOrder: false);
    }

    [Fact]
    public async Task A_token_that_keycloak_refuses_should_be_replaced_and_the_request_repeated_once()
    {
        _stub.On(HttpMethod.Post, TokenPath, Reply.Token("t1"), Reply.Token("t2"));
        _stub.On(HttpMethod.Delete, AccountPath, Reply.Status(HttpStatusCode.Unauthorized), Reply.Status(HttpStatusCode.NoContent));

        await Provider().DeleteUserAsync("abc-123", _ct);

        _stub.Requests.Where(request => request.Method == HttpMethod.Delete)
            .Select(request => request.Token).ShouldBe(["t1", "t2"], ignoreOrder: false);
    }

    [Fact]
    public async Task An_account_should_be_created_with_the_profile_and_get_the_first_access_email()
    {
        _stub.On(HttpMethod.Post, UsersPath, Reply.Created("http://keycloak.test" + AccountPath));
        _stub.On(HttpMethod.Put, AccountPath + "/execute-actions-email", Reply.Status(HttpStatusCode.NoContent));

        var result = await Provider().CreateUserAsync(Profile(Language.PortugueseBrazil), _ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("abc-123");
        var body = BodyOf(Single(HttpMethod.Post, UsersPath));
        body["username"]!.GetValue<string>().ShouldBe("bia@example.com");
        body["email"]!.GetValue<string>().ShouldBe("bia@example.com");
        body["firstName"]!.GetValue<string>().ShouldBe("Bia Lopes");
        body["enabled"]!.GetValue<bool>().ShouldBeTrue();
        body["attributes"]!["locale"]![0]!.GetValue<string>().ShouldBe("pt-BR");
        var email = Single(HttpMethod.Put, AccountPath + "/execute-actions-email");
        email.Query.ShouldBe("?lifespan=259200");
        email.Body.ShouldBe("""["UPDATE_PASSWORD"]""");
    }

    [Fact]
    public async Task Without_a_language_and_without_the_email_the_account_should_be_created_plainly()
    {
        _stub.On(HttpMethod.Post, UsersPath, Reply.Created("http://keycloak.test" + AccountPath));

        var result = await Provider(options => options.SendSetupEmail = false)
            .CreateUserAsync(Profile(), _ct);

        result.IsSuccess.ShouldBeTrue();
        BodyOf(Single(HttpMethod.Post, UsersPath))["attributes"].ShouldBeNull();
        _stub.Requests.ShouldNotContain(request => request.Path.EndsWith("execute-actions-email"));
    }

    [Fact]
    public async Task An_email_already_in_use_should_be_a_conflict_and_send_nothing_else()
    {
        _stub.On(HttpMethod.Post, UsersPath, Reply.Json(HttpStatusCode.Conflict, """{"errorMessage":"User exists with same email"}"""));

        var result = await Provider().CreateUserAsync(Profile(), _ct);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Identity.EmailAlreadyInUse");
        _stub.Requests.Count(request => request.Path.StartsWith(UsersPath)).ShouldBe(1);
    }

    [Fact]
    public async Task An_email_that_cannot_be_sent_should_delete_the_account_that_was_created()
    {
        _stub.On(HttpMethod.Post, UsersPath, Reply.Created("http://keycloak.test" + AccountPath));
        _stub.On(HttpMethod.Put, AccountPath + "/execute-actions-email", Reply.Status(HttpStatusCode.InternalServerError, "smtp"));
        _stub.On(HttpMethod.Delete, AccountPath, Reply.Status(HttpStatusCode.NoContent));

        await Should.ThrowAsync<IdentityProviderException>(() => Provider().CreateUserAsync(Profile(), _ct));

        _stub.Requests.ShouldContain(request => request.Method == HttpMethod.Delete && request.Path == AccountPath);
    }

    [Fact]
    public async Task An_answer_keycloak_gives_for_no_reason_the_caller_can_act_on_should_throw()
    {
        _stub.On(HttpMethod.Post, UsersPath, Reply.Status(HttpStatusCode.InternalServerError, "boom"));

        var exception = await Should.ThrowAsync<IdentityProviderException>(() => Provider().CreateUserAsync(Profile(), _ct));

        exception.Message.ShouldContain("500");
    }

    [Fact]
    public async Task An_update_should_send_the_whole_account_with_only_the_profile_changed()
    {
        _stub.On(HttpMethod.Get, AccountPath, Reply.Json(HttpStatusCode.OK, StoredAccount));
        _stub.On(HttpMethod.Put, AccountPath, Reply.Status(HttpStatusCode.NoContent));

        var result = await Provider().UpdateUserAsync("abc-123", Profile(Language.Spanish), _ct);

        result.IsSuccess.ShouldBeTrue();
        var body = BodyOf(Single(HttpMethod.Put, AccountPath));
        body["email"]!.GetValue<string>().ShouldBe("bia@example.com");
        body["username"]!.GetValue<string>().ShouldBe("bia@example.com");
        body["firstName"]!.GetValue<string>().ShouldBe("Bia Lopes");
        body["lastName"]!.GetValue<string>().ShouldBe("Name");
        body["enabled"]!.GetValue<bool>().ShouldBeTrue();
        body["attributes"]!["locale"]![0]!.GetValue<string>().ShouldBe("es");
        body["attributes"]!["phone"]![0]!.GetValue<string>().ShouldBe("+5511");
    }

    [Fact]
    public async Task An_update_without_a_language_should_remove_the_locale_and_keep_the_other_attributes()
    {
        _stub.On(HttpMethod.Get, AccountPath, Reply.Json(HttpStatusCode.OK, StoredAccount));
        _stub.On(HttpMethod.Put, AccountPath, Reply.Status(HttpStatusCode.NoContent));

        await Provider().UpdateUserAsync("abc-123", Profile(), _ct);

        var body = BodyOf(Single(HttpMethod.Put, AccountPath));
        body["attributes"]!["locale"].ShouldBeNull();
        body["attributes"]!["phone"]![0]!.GetValue<string>().ShouldBe("+5511");
    }

    [Fact]
    public async Task An_update_to_an_email_in_use_should_be_a_conflict()
    {
        _stub.On(HttpMethod.Get, AccountPath, Reply.Json(HttpStatusCode.OK, StoredAccount));
        _stub.On(HttpMethod.Put, AccountPath, Reply.Json(HttpStatusCode.Conflict, """{"errorMessage":"User exists with same email"}"""));

        var result = await Provider().UpdateUserAsync("abc-123", Profile(), _ct);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Identity.EmailAlreadyInUse");
    }

    [Fact]
    public async Task An_update_of_an_account_that_does_not_exist_should_throw()
    {
        _stub.On(HttpMethod.Get, AccountPath, Reply.Status(HttpStatusCode.NotFound));

        await Should.ThrowAsync<IdentityProviderException>(() => Provider().UpdateUserAsync("abc-123", Profile(), _ct));
    }

    [Fact]
    public async Task Disabling_an_account_should_send_it_back_whole_with_only_enabled_changed()
    {
        _stub.On(HttpMethod.Get, AccountPath, Reply.Json(HttpStatusCode.OK, StoredAccount));
        _stub.On(HttpMethod.Put, AccountPath, Reply.Status(HttpStatusCode.NoContent));

        await Provider().SetEnabledAsync("abc-123", false, _ct);

        var body = BodyOf(Single(HttpMethod.Put, AccountPath));
        body["enabled"]!.GetValue<bool>().ShouldBeFalse();
        body["lastName"]!.GetValue<string>().ShouldBe("Name");
        body["attributes"]!["locale"]![0]!.GetValue<string>().ShouldBe("en");
    }

    [Fact]
    public async Task Deleting_an_account_that_is_already_gone_should_not_fail()
    {
        _stub.On(HttpMethod.Delete, AccountPath, Reply.Status(HttpStatusCode.NotFound));

        await Should.NotThrowAsync(() => Provider().DeleteUserAsync("abc-123", _ct));
    }
}