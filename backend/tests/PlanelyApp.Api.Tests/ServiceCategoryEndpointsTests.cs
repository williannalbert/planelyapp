using System.Net;
using Microsoft.AspNetCore.Builder;
using PlanelyApp.Api.Endpoints.Catalog.ServiceCategories;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.ServiceCategories;
using PlanelyApp.Application.Catalog.ServiceCategories.Create;
using PlanelyApp.Application.Catalog.ServiceCategories.List;
using PlanelyApp.Application.Catalog.ServiceCategories.Rename;
using PlanelyApp.Domain.Organization.Users;
using Shouldly;

namespace PlanelyApp.Api.Tests;

/// <summary>
/// The category endpoints read the route and the body, send one command or query and turn the result into HTTP. The category of the
/// route is the one the command gets, never one from the body. There is no read of a single category, so the 201 of a creation points to
/// the list, where it is read. The rules of the category are in the use cases, which have their own tests.
/// </summary>
public class ServiceCategoryEndpointsTests
{
    private static readonly Guid CategoryId = Guid.CreateVersion7();
    private static readonly Guid NewCategoryId = Guid.CreateVersion7();
    private static readonly Guid OtherCompany = Guid.CreateVersion7();

    private static readonly string Base = $"/companies/{StaffApi.Company}/service-categories";
    private static readonly string One = $"{Base}/{CategoryId}";

    private const string CreateJson = """{ "name": "Hair" }""";
    private const string RenameJson = """{ "serviceCategoryId": "00000000-0000-0000-0000-000000000001", "name": "Hair and beard" }""";

    private static readonly IReadOnlyList<ServiceCategoryDto> Categories = [new(CategoryId, "Hair")];

    // The services are what the whole staff offers and books; what the catalog is made of is for the administrator.
    private static readonly UserRole[] CompanyRoles = [UserRole.CompanyAdmin, UserRole.Receptionist, UserRole.Professional];
    private static readonly UserRole[] AdminOnly = [UserRole.CompanyAdmin];

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    /// <summary>Which roles each endpoint accepts, and what it answers when it works.</summary>
    private static readonly Dictionary<string, (UserRole[] Roles, HttpStatusCode Success)> Rules = new()
    {
        ["list"] = (CompanyRoles, HttpStatusCode.OK),
        ["create"] = (AdminOnly, HttpStatusCode.Created),
        ["rename"] = (AdminOnly, HttpStatusCode.NoContent),
    };

    public static TheoryData<string, UserRole> EveryEndpointWithEveryRole()
    {
        var data = new TheoryData<string, UserRole>();

        foreach (var name in Rules.Keys)
        {
            foreach (var role in Enum.GetValues<UserRole>())
            {
                data.Add(name, role);
            }
        }

        return data;
    }

    public static TheoryData<string> EveryEndpoint()
    {
        var data = new TheoryData<string>();

        foreach (var name in Rules.Keys)
        {
            data.Add(name);
        }

        return data;
    }

    private static FakeDispatcher WithReplies(FakeDispatcher dispatcher) => dispatcher
        .Reply<ListServiceCategoriesQuery, IReadOnlyList<ServiceCategoryDto>>(
            _ => Result<IReadOnlyList<ServiceCategoryDto>>.Success(Categories))
        .Reply<CreateServiceCategoryCommand, Guid>(_ => NewCategoryId)
        .Reply<RenameServiceCategoryCommand, Unit>(_ => Unit.Value);

    private static FakeDispatcher Answering(UserRole role) => WithReplies(StaffApi.As(role));

    private static (HttpMethod Method, string Path, string? Json) Route(string name, Guid? company = null)
    {
        var root = $"/companies/{company ?? StaffApi.Company}/service-categories";

        return name switch
        {
            "list" => (HttpMethod.Get, root, null),
            "create" => (HttpMethod.Post, root, CreateJson),
            "rename" => (HttpMethod.Put, $"{root}/{CategoryId}", RenameJson),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };
    }

    private static Task<WebApplication> StartAsync(FakeDispatcher dispatcher) =>
        StaffApi.StartAsync(dispatcher, api => api.MapServiceCategories());

    private Task<HttpResponseMessage> SendAsync(WebApplication app, string name, bool signedIn = true, Guid? company = null)
    {
        var (method, path, json) = Route(name, company);

        return StaffApi.SendAsync(app, method, path, _ct, json is null ? null : StaffApi.Json(json), signedIn);
    }

    // Role and status ---------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(EveryEndpointWithEveryRole))]
    public async Task Each_endpoint_should_accept_only_its_roles_and_answer_with_its_status(string name, UserRole role)
    {
        var dispatcher = Answering(role);
        await using var app = await StartAsync(dispatcher);
        var (roles, success) = Rules[name];

        var response = await SendAsync(app, name);

        if (roles.Contains(role))
        {
            response.StatusCode.ShouldBe(success);
            dispatcher.Requests.Count.ShouldBe(1);
        }
        else
        {
            var body = await TestApi.ReadAsync(response, _ct);

            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            body.GetProperty("code").GetString().ShouldBe("User.RoleNotAllowed");
            dispatcher.Requests.ShouldBeEmpty();
        }
    }

    [Theory]
    [MemberData(nameof(EveryEndpoint))]
    public async Task Without_a_token_every_endpoint_should_answer_401_and_send_nothing(string name)
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, name, signedIn: false);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        dispatcher.Requests.ShouldBeEmpty();
        dispatcher.Asked.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(EveryEndpoint))]
    public async Task A_company_administrator_should_be_refused_in_another_company(string name)
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, name, company: OtherCompany);
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        body.GetProperty("code").GetString().ShouldBe("Company.AccessDenied");
        dispatcher.Requests.ShouldBeEmpty();
    }

    // List, create and rename -------------------------------------------------------------------------------------

    [Fact]
    public async Task Listing_should_answer_an_array_in_camel_case()
    {
        var dispatcher = Answering(UserRole.Professional);
        await using var app = await StartAsync(dispatcher);

        var body = await TestApi.ReadAsync(await SendAsync(app, "list"), _ct);

        dispatcher.Requests.Single().ShouldBe(new ListServiceCategoriesQuery());
        var item = body.EnumerateArray().Single();
        item.GetProperty("id").GetGuid().ShouldBe(CategoryId);
        item.GetProperty("name").GetString().ShouldBe("Hair");
    }

    [Fact]
    public async Task Creating_should_send_the_body_as_the_command_and_answer_201_with_the_list_as_the_location()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "create");
        var body = await TestApi.ReadAsync(response, _ct);

        // A category is read inside the list: there is no address for one.
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location!.OriginalString.ShouldBe(Base);
        body.GetProperty("id").GetGuid().ShouldBe(NewCategoryId);
        dispatcher.Requests.Single().ShouldBe(new CreateServiceCategoryCommand("Hair"));
    }

    [Fact]
    public async Task A_name_that_is_already_used_should_be_a_409_with_its_code()
    {
        var dispatcher = StaffApi.As(UserRole.CompanyAdmin)
            .Reply<CreateServiceCategoryCommand, Guid>(_ => Error.Conflict("ServiceCategory.NameAlreadyExists"));
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "create");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        body.GetProperty("code").GetString().ShouldBe("ServiceCategory.NameAlreadyExists");
    }

    [Fact]
    public async Task Renaming_should_use_the_category_of_the_route_and_ignore_one_in_the_body()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "rename");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(new RenameServiceCategoryCommand(CategoryId, "Hair and beard"));
    }

    [Fact]
    public async Task A_category_that_does_not_exist_should_be_a_404_with_its_code()
    {
        var dispatcher = StaffApi.As(UserRole.CompanyAdmin)
            .Reply<RenameServiceCategoryCommand, Unit>(_ => Error.NotFound("ServiceCategory.NotFound"));
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "rename");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.GetProperty("code").GetString().ShouldBe("ServiceCategory.NotFound");
    }
}
