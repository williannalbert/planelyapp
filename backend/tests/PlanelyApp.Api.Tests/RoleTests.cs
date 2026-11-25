using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PlanelyApp.Api.Roles;
using PlanelyApp.Domain.Organization.Users;
using Shouldly;

namespace PlanelyApp.Api.Tests;

/// <summary>
/// What each role may do is declared on the endpoint, and an endpoint that declares nothing is closed: forgetting the rule
/// can never open a door. The check comes after the authentication and the tenancy, and before the endpoint.
/// </summary>
public class RoleTests
{
    private readonly CapturingLoggerProvider _logs = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private Task<WebApplication> StartAsync(FakeDispatcher dispatcher, List<string> reached) => StaffApi.StartAsync(
        dispatcher,
        api =>
        {
            IResult Reach(string route)
            {
                reached.Add(route);

                return Results.Ok();
            }

            api.MapGet("/roles/any", () => Reach("any")).RequireAnyStaff();
            api.MapGet("/roles/platform", () => Reach("platform")).RequireRoles(UserRole.PlatformAdmin);
            api.MapGet("/roles/company-admin", () => Reach("company-admin")).RequireRoles(UserRole.CompanyAdmin);
            api.MapGet("/roles/front-desk", () => Reach("front-desk")).RequireRoles(UserRole.CompanyAdmin, UserRole.Receptionist);
            api.MapGet("/roles/undeclared", () => Reach("undeclared"));
            api.MapGet("/roles/open", () => Reach("open")).AllowAnonymous();

            var group = api.MapGroup("/roles/group").RequireRoles(UserRole.Receptionist);
            group.MapGet("/inherited", () => Reach("inherited"));
            group.MapGet("/override", () => Reach("override")).RequireRoles(UserRole.CompanyAdmin);
        },
        builder => builder.Logging.AddProvider(_logs));

    private Task<HttpResponseMessage> GetAsync(WebApplication app, string path, bool signedIn = true, string? language = null) =>
        StaffApi.SendAsync(app, HttpMethod.Get, path, _ct, signedIn: signedIn, language: language);

    // The matrix --------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(UserRole.PlatformAdmin, "/roles/any", HttpStatusCode.OK)]
    [InlineData(UserRole.CompanyAdmin, "/roles/any", HttpStatusCode.OK)]
    [InlineData(UserRole.Receptionist, "/roles/any", HttpStatusCode.OK)]
    [InlineData(UserRole.Professional, "/roles/any", HttpStatusCode.OK)]
    [InlineData(UserRole.PlatformAdmin, "/roles/platform", HttpStatusCode.OK)]
    [InlineData(UserRole.CompanyAdmin, "/roles/platform", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.Receptionist, "/roles/platform", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.Professional, "/roles/platform", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.PlatformAdmin, "/roles/company-admin", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.CompanyAdmin, "/roles/company-admin", HttpStatusCode.OK)]
    [InlineData(UserRole.Receptionist, "/roles/company-admin", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.Professional, "/roles/company-admin", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.PlatformAdmin, "/roles/front-desk", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.CompanyAdmin, "/roles/front-desk", HttpStatusCode.OK)]
    [InlineData(UserRole.Receptionist, "/roles/front-desk", HttpStatusCode.OK)]
    [InlineData(UserRole.Professional, "/roles/front-desk", HttpStatusCode.Forbidden)]
    public async Task Each_endpoint_should_accept_only_the_roles_it_names(UserRole role, string path, HttpStatusCode expected)
    {
        var reached = new List<string>();
        await using var app = await StartAsync(StaffApi.As(role), reached);

        var response = await GetAsync(app, path);

        response.StatusCode.ShouldBe(expected);
        reached.Count.ShouldBe(expected == HttpStatusCode.OK ? 1 : 0);
    }

    [Fact]
    public async Task Every_role_should_pass_where_any_staff_is_accepted()
    {
        foreach (var role in Enum.GetValues<UserRole>())
        {
            await using var app = await StartAsync(StaffApi.As(role), []);

            (await GetAsync(app, "/roles/any")).StatusCode.ShouldBe(HttpStatusCode.OK, role.ToString());
        }
    }

    [Fact]
    public async Task A_role_that_is_not_allowed_should_be_a_403_with_the_code_and_the_text_of_the_language()
    {
        await using var app = await StartAsync(StaffApi.As(UserRole.Professional), []);

        var portuguese = await TestApi.ReadAsync(await GetAsync(app, "/roles/platform"), _ct);
        var english = await TestApi.ReadAsync(await GetAsync(app, "/roles/platform", language: "en"), _ct);
        var spanish = await TestApi.ReadAsync(await GetAsync(app, "/roles/platform", language: "es"), _ct);

        portuguese.GetProperty("code").GetString().ShouldBe("User.RoleNotAllowed");
        portuguese.GetProperty("status").GetInt32().ShouldBe(403);
        portuguese.GetProperty("detail").GetString().ShouldBe("Seu papel não permite esta operação.");
        english.GetProperty("detail").GetString().ShouldBe("Your role does not allow this operation.");
        spanish.GetProperty("detail").GetString().ShouldBe("Tu rol no permite esta operación.");
    }

    [Fact]
    public async Task A_refusal_should_be_logged_with_ids_and_the_role_only()
    {
        var dispatcher = StaffApi.As(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher, []);

        await GetAsync(app, "/roles/platform");

        var entry = _logs.Entries.Single(candidate => candidate.Category.EndsWith(nameof(RoleMiddleware)));
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Message.ShouldContain("User.RoleNotAllowed");
        entry.Message.ShouldContain("Receptionist");
    }

    // Closed by default -------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_endpoint_with_no_role_rule_should_be_closed_and_the_mistake_should_be_logged_as_an_error()
    {
        var reached = new List<string>();
        await using var app = await StartAsync(StaffApi.As(UserRole.CompanyAdmin), reached);

        var response = await GetAsync(app, "/roles/undeclared");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        body.GetProperty("code").GetString().ShouldBe("User.RoleNotAllowed");
        reached.ShouldBeEmpty();

        var entry = _logs.Entries.Single(candidate => candidate.Category.EndsWith(nameof(RoleMiddleware)));
        entry.Level.ShouldBe(LogLevel.Error);
        entry.Message.ShouldContain("no role rule");
    }

    [Fact]
    public async Task An_open_endpoint_should_not_ask_for_a_role_nor_for_the_user()
    {
        var dispatcher = StaffApi.As(UserRole.CompanyAdmin);
        var reached = new List<string>();
        await using var app = await StartAsync(dispatcher, reached);

        var response = await GetAsync(app, "/roles/open", signedIn: false);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        reached.ShouldBe(["open"]);
        dispatcher.Asked.ShouldBeEmpty();
        _logs.Entries.Where(candidate => candidate.Category.EndsWith(nameof(RoleMiddleware))).ShouldBeEmpty();
    }

    [Fact]
    public async Task Without_a_token_the_answer_should_be_a_401_before_any_role_is_looked_at()
    {
        var reached = new List<string>();
        await using var app = await StartAsync(StaffApi.As(UserRole.CompanyAdmin), reached);

        var response = await GetAsync(app, "/roles/any", signedIn: false);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        reached.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_user_who_is_not_registered_should_hear_that_before_anything_about_roles()
    {
        var reached = new List<string>();
        await using var app = await StartAsync(new FakeDispatcher(), reached);

        var response = await GetAsync(app, "/roles/any");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        body.GetProperty("code").GetString().ShouldBe("User.NotRegistered");
        reached.ShouldBeEmpty();
    }

    // Groups ------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(UserRole.Receptionist, "/roles/group/inherited", HttpStatusCode.OK)]
    [InlineData(UserRole.CompanyAdmin, "/roles/group/inherited", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.CompanyAdmin, "/roles/group/override", HttpStatusCode.OK)]
    [InlineData(UserRole.Receptionist, "/roles/group/override", HttpStatusCode.Forbidden)]
    public async Task The_rule_of_a_group_should_apply_to_its_endpoints_and_the_rule_of_an_endpoint_should_replace_it(
        UserRole role, string path, HttpStatusCode expected)
    {
        await using var app = await StartAsync(StaffApi.As(role), []);

        (await GetAsync(app, path)).StatusCode.ShouldBe(expected);
    }

    // The declaration ---------------------------------------------------------------------------------------------

    [Fact]
    public void Naming_no_role_should_be_a_mistake_and_not_a_door_that_nobody_can_pass()
    {
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();

        Should.Throw<ArgumentException>(() => app.MapGet("/x", () => "x").RequireRoles());
    }
}
