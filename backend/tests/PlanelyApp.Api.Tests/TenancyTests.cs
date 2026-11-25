using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Api.Authentication;
using PlanelyApp.Api.Tenancy;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Organization.Users.ResolveActor;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Infrastructure.Tenancy;
using Shouldly;

namespace PlanelyApp.Api.Tests;

/// <summary>
/// The company comes from the route and nothing else (RN23): a user works only in its own company, and the platform
/// administrator in any. A staff member sees only the branches of the list it was given (RN11). The checks come after the
/// authentication and before the endpoint, so a refused request never reaches a use case.
/// </summary>
public class TenancyTests
{
    private static readonly Guid MyCompany = Guid.CreateVersion7();
    private static readonly Guid OtherCompany = Guid.CreateVersion7();
    private static readonly Guid MyBranch = Guid.CreateVersion7();
    private static readonly Guid OtherBranch = Guid.CreateVersion7();

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static FakeDispatcher Staff(Guid? company, UserRole role, IReadOnlyList<Guid>? branches) =>
        new() { Resolve = _ => new StaffActor(Guid.CreateVersion7(), company, role, branches) };

    private static FakeDispatcher CompanyAdmin() => Staff(MyCompany, UserRole.CompanyAdmin, null);

    /// <summary>Every endpoint writes its path in <paramref name="reached"/>, so a test can tell that a refused request never got there.</summary>
    private static Task<WebApplication> StartAsync(FakeDispatcher dispatcher, List<string> reached) => TestApi.StartAsync(
        api =>
        {
            api.UseApiAuthentication();
            api.UseApiTenancy();

            IResult Answer(string route, TenantContext tenant)
            {
                reached.Add(route);

                return Results.Ok(new { company = tenant.CompanyId });
            }

            api.MapGet("/companies/{companyId:guid}/ping", (TenantContext tenant) => Answer("company", tenant));
            api.MapGet("/companies/{companyId:guid}/branches/{branchId:guid}/ping", (TenantContext tenant) => Answer("branch", tenant));
            api.MapGet("/companies/{companyId:guid}/public", (TenantContext tenant) => Answer("public", tenant)).AllowAnonymous();
            api.MapGet("/branches/{branchId:guid}/ping", (TenantContext tenant) => Answer("branch-only", tenant));
            api.MapGet("/platform/ping", (TenantContext tenant) => Answer("platform", tenant));

            // No :guid constraint: the middleware is the one that has to refuse a value that is not an id.
            api.MapGet("/loose/{companyId}/ping", (TenantContext tenant) => Answer("loose", tenant));
        },
        builder =>
        {
            builder.Services.AddSingleton<IDispatcher>(dispatcher);
            builder.Services.AddScoped<TenantContext>();
            builder.Services.AddApiAuthentication(TestIdentity.Keycloak, builder.Environment);
            builder.Services.Configure<JwtBearerOptions>(
                ApiAuthenticationExtensions.StaffScheme, options => options.ConfigurationManager = TestIdentity.Metadata);
        });

    private async Task<HttpResponseMessage> GetAsync(WebApplication app, string path, bool signedIn = true, string? language = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (signedIn)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestIdentity.Token());
        }

        if (language is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", language);
        }

        return await app.GetTestClient().SendAsync(request, _ct);
    }

    private async Task<Guid?> CompanyOfAsync(HttpResponseMessage response)
    {
        var body = await TestApi.ReadAsync(response, _ct);
        var company = body.GetProperty("company");

        return company.ValueKind == JsonValueKind.Null ? null : company.GetGuid();
    }

    // The company -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_user_should_work_in_its_own_company_and_the_request_should_carry_it()
    {
        var reached = new List<string>();
        await using var app = await StartAsync(CompanyAdmin(), reached);

        var response = await GetAsync(app, $"/companies/{MyCompany}/ping");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await CompanyOfAsync(response)).ShouldBe(MyCompany);
    }

    [Fact]
    public async Task A_user_should_be_refused_in_another_company_and_the_endpoint_should_not_run()
    {
        var reached = new List<string>();
        await using var app = await StartAsync(CompanyAdmin(), reached);

        var response = await GetAsync(app, $"/companies/{OtherCompany}/ping");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        body.GetProperty("code").GetString().ShouldBe("Company.AccessDenied");
        body.GetProperty("detail").GetString().ShouldBe("Você não tem acesso a esta empresa.");
        reached.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_refusal_should_speak_the_language_of_the_request()
    {
        await using var app = await StartAsync(CompanyAdmin(), []);

        var response = await GetAsync(app, $"/companies/{OtherCompany}/ping", language: "en");
        var body = await TestApi.ReadAsync(response, _ct);

        body.GetProperty("detail").GetString().ShouldBe("You do not have access to this company.");
    }

    [Fact]
    public async Task The_platform_administrator_should_work_in_any_company()
    {
        await using var app = await StartAsync(Staff(null, UserRole.PlatformAdmin, []), []);

        var response = await GetAsync(app, $"/companies/{OtherCompany}/ping");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await CompanyOfAsync(response)).ShouldBe(OtherCompany);
    }

    [Fact]
    public async Task A_route_with_no_company_should_leave_the_request_without_one()
    {
        await using var app = await StartAsync(CompanyAdmin(), []);

        var response = await GetAsync(app, "/platform/ping");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await CompanyOfAsync(response)).ShouldBeNull();
    }

    [Fact]
    public async Task A_request_with_no_token_to_a_closed_route_should_be_a_401_and_not_a_403()
    {
        var reached = new List<string>();
        await using var app = await StartAsync(CompanyAdmin(), reached);

        var response = await GetAsync(app, $"/companies/{MyCompany}/ping", signedIn: false);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        reached.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_open_route_of_a_company_should_carry_the_company_with_no_token()
    {
        var dispatcher = CompanyAdmin();
        await using var app = await StartAsync(dispatcher, []);

        var response = await GetAsync(app, $"/companies/{OtherCompany}/public", signedIn: false);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await CompanyOfAsync(response)).ShouldBe(OtherCompany);
        dispatcher.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_open_route_should_not_compare_the_company_of_a_user_who_happens_to_send_a_token()
    {
        var dispatcher = CompanyAdmin();
        await using var app = await StartAsync(dispatcher, []);

        var response = await GetAsync(app, $"/companies/{OtherCompany}/public");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await CompanyOfAsync(response)).ShouldBe(OtherCompany);
        dispatcher.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task Each_request_should_have_the_company_of_its_own_route()
    {
        await using var app = await StartAsync(Staff(null, UserRole.PlatformAdmin, []), []);

        var first = await CompanyOfAsync(await GetAsync(app, $"/companies/{MyCompany}/ping"));
        var second = await CompanyOfAsync(await GetAsync(app, $"/companies/{OtherCompany}/ping"));

        first.ShouldBe(MyCompany);
        second.ShouldBe(OtherCompany);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task A_company_in_the_route_that_is_not_an_id_should_be_a_404(string value)
    {
        var reached = new List<string>();
        await using var app = await StartAsync(Staff(null, UserRole.PlatformAdmin, []), reached);

        var response = await GetAsync(app, $"/loose/{value}/ping");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.GetProperty("code").GetString().ShouldBe("Http.NotFound");
        reached.ShouldBeEmpty();
    }

    // The branch --------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("every branch", true)]
    [InlineData("a list with the branch", true)]
    [InlineData("a list without the branch", false)]
    [InlineData("an empty list", false)]
    public async Task A_staff_member_should_see_only_the_branches_of_its_list(string access, bool allowed)
    {
        IReadOnlyList<Guid>? branches = access switch
        {
            "every branch" => null,
            "a list with the branch" => [OtherBranch, MyBranch],
            "a list without the branch" => [OtherBranch],
            "an empty list" => [],
            _ => throw new ArgumentOutOfRangeException(nameof(access), access, null),
        };

        var reached = new List<string>();
        await using var app = await StartAsync(Staff(MyCompany, UserRole.Receptionist, branches), reached);

        var response = await GetAsync(app, $"/companies/{MyCompany}/branches/{MyBranch}/ping");

        if (allowed)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK, access);
            reached.ShouldBe(["branch"], access);

            return;
        }

        var body = await TestApi.ReadAsync(response, _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, access);
        body.GetProperty("code").GetString().ShouldBe("Branch.AccessDenied");
        reached.ShouldBeEmpty(access);
    }

    [Fact]
    public async Task The_platform_administrator_should_have_no_branch_even_in_a_company_it_may_enter()
    {
        await using var app = await StartAsync(Staff(null, UserRole.PlatformAdmin, []), []);

        var response = await GetAsync(app, $"/companies/{MyCompany}/branches/{MyBranch}/ping");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        body.GetProperty("code").GetString().ShouldBe("Branch.AccessDenied");
    }

    [Fact]
    public async Task The_company_should_be_checked_before_the_branch()
    {
        await using var app = await StartAsync(Staff(MyCompany, UserRole.Receptionist, [OtherBranch]), []);

        var response = await GetAsync(app, $"/companies/{OtherCompany}/branches/{MyBranch}/ping");
        var body = await TestApi.ReadAsync(response, _ct);

        body.GetProperty("code").GetString().ShouldBe("Company.AccessDenied");
    }

    [Fact]
    public async Task A_route_with_a_branch_and_no_company_should_still_check_the_branch()
    {
        await using var app = await StartAsync(Staff(MyCompany, UserRole.Receptionist, [OtherBranch]), []);

        var denied = await GetAsync(app, $"/branches/{MyBranch}/ping");
        var allowed = await GetAsync(app, $"/branches/{OtherBranch}/ping");

        denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
