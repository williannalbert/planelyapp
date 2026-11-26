using System.Net;
using Microsoft.AspNetCore.Builder;
using PlanelyApp.Api.Endpoints.Catalog.ServiceOfferings;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.ServiceOfferings;
using PlanelyApp.Application.Catalog.ServiceOfferings.Activate;
using PlanelyApp.Application.Catalog.ServiceOfferings.ChangePricing;
using PlanelyApp.Application.Catalog.ServiceOfferings.Create;
using PlanelyApp.Application.Catalog.ServiceOfferings.Deactivate;
using PlanelyApp.Application.Catalog.ServiceOfferings.Get;
using PlanelyApp.Application.Catalog.ServiceOfferings.List;
using PlanelyApp.Application.Catalog.ServiceOfferings.UpdateDetails;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Users;
using Shouldly;

namespace PlanelyApp.Api.Tests;

/// <summary>
/// The service endpoints read the route, the query string and the body, send one command or query and turn the result into HTTP. The
/// service of the route is the one the command gets, never one from the body. The price travels as a JSON number and arrives as a
/// <c>decimal</c>, so 45.90 is 45.90 and not the nearest binary fraction. The rules of the service are in the use cases.
/// </summary>
public class ServiceOfferingEndpointsTests
{
    private static readonly Guid ServiceId = Guid.CreateVersion7();
    private static readonly Guid NewServiceId = Guid.CreateVersion7();
    private static readonly Guid CategoryId = Guid.CreateVersion7();
    private static readonly Guid OtherCompany = Guid.CreateVersion7();

    private static readonly string Base = $"/companies/{StaffApi.Company}/service-offerings";
    private static readonly string One = $"{Base}/{ServiceId}";

    private static readonly string CreateJson = $$"""
        { "name": "Haircut", "durationMinutes": 45, "pricing": { "type": "Fixed", "basePrice": 45.90 },
          "description": "Classic cut", "categoryId": "{{CategoryId}}" }
        """;

    private static readonly string UpdateJson = $$"""
        { "serviceOfferingId": "00000000-0000-0000-0000-000000000001", "name": "Haircut Plus", "durationMinutes": 60,
          "description": "Cut and beard", "categoryId": "{{CategoryId}}" }
        """;

    private const string PricingJson = """{ "type": "Fixed", "basePrice": 60 }""";

    private static readonly PagedResult<ServiceOfferingSummaryDto> Page = new(
        [new ServiceOfferingSummaryDto(ServiceId, "Haircut", CategoryId, "Hair", 45, PricingType.Fixed, 50m, true)], 2, 50, 101);

    private static readonly ServiceOfferingDto Service = new(
        ServiceId, "Haircut", "Classic cut", 45, PricingType.Fixed, 50m, CategoryId, true);

    // The services are what the whole staff offers and books; what the catalog is made of is for the administrator.
    private static readonly UserRole[] CompanyRoles = [UserRole.CompanyAdmin, UserRole.Receptionist, UserRole.Professional];
    private static readonly UserRole[] AdminOnly = [UserRole.CompanyAdmin];

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    /// <summary>Which roles each endpoint accepts, and what it answers when it works.</summary>
    private static readonly Dictionary<string, (UserRole[] Roles, HttpStatusCode Success)> Rules = new()
    {
        ["list"] = (CompanyRoles, HttpStatusCode.OK),
        ["create"] = (AdminOnly, HttpStatusCode.Created),
        ["get"] = (CompanyRoles, HttpStatusCode.OK),
        ["update"] = (AdminOnly, HttpStatusCode.NoContent),
        ["change-pricing"] = (AdminOnly, HttpStatusCode.NoContent),
        ["activate"] = (AdminOnly, HttpStatusCode.NoContent),
        ["deactivate"] = (AdminOnly, HttpStatusCode.NoContent),
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
        .Reply<ListServiceOfferingsQuery, PagedResult<ServiceOfferingSummaryDto>>(_ => Page)
        .Reply<GetServiceOfferingQuery, ServiceOfferingDto>(_ => Service)
        .Reply<CreateServiceOfferingCommand, Guid>(_ => NewServiceId)
        .Reply<UpdateServiceOfferingDetailsCommand, Unit>(_ => Unit.Value)
        .Reply<ChangeServiceOfferingPricingCommand, Unit>(_ => Unit.Value)
        .Reply<ActivateServiceOfferingCommand, Unit>(_ => Unit.Value)
        .Reply<DeactivateServiceOfferingCommand, Unit>(_ => Unit.Value);

    private static FakeDispatcher Answering(UserRole role) => WithReplies(StaffApi.As(role));

    private static (HttpMethod Method, string Path, string? Json) Route(string name, Guid? company = null)
    {
        var root = $"/companies/{company ?? StaffApi.Company}/service-offerings";
        var one = $"{root}/{ServiceId}";

        return name switch
        {
            "list" => (HttpMethod.Get, root, null),
            "create" => (HttpMethod.Post, root, CreateJson),
            "get" => (HttpMethod.Get, one, null),
            "update" => (HttpMethod.Put, one, UpdateJson),
            "change-pricing" => (HttpMethod.Put, $"{one}/pricing", PricingJson),
            "activate" => (HttpMethod.Post, $"{one}/activate", null),
            "deactivate" => (HttpMethod.Post, $"{one}/deactivate", null),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };
    }

    private static Task<WebApplication> StartAsync(FakeDispatcher dispatcher) =>
        StaffApi.StartAsync(dispatcher, api => api.MapServiceOfferings());

    private Task<HttpResponseMessage> SendAsync(WebApplication app, string name, bool signedIn = true, Guid? company = null)
    {
        var (method, path, json) = Route(name, company);

        return StaffApi.SendAsync(app, method, path, _ct, json is null ? null : StaffApi.Json(json), signedIn);
    }

    private Task<HttpResponseMessage> CallAsync(
        WebApplication app, HttpMethod method, string path, string? json = null) =>
        StaffApi.SendAsync(app, method, path, _ct, json is null ? null : StaffApi.Json(json));

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

    // List and get ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("", null, null, 1, PageLimits.DefaultPageSize)]
    [InlineData("?search=hair&isActive=true&page=2&pageSize=50", "hair", true, 2, 50)]
    public async Task Listing_should_pass_the_filters_and_the_page_of_the_query_string_with_the_defaults_of_the_use_case(
        string query, string? search, bool? isActive, int page, int pageSize)
    {
        var dispatcher = Answering(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Get, Base + query);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        dispatcher.Requests.Single().ShouldBe(new ListServiceOfferingsQuery(search, null, isActive, page, pageSize));
    }

    [Fact]
    public async Task Listing_should_pass_the_category_of_the_query_string()
    {
        var dispatcher = Answering(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Get, $"{Base}?categoryId={CategoryId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        dispatcher.Requests.Single().ShouldBe(new ListServiceOfferingsQuery(CategoryId: CategoryId));
    }

    [Theory]
    [InlineData("?page=two")]
    [InlineData("?categoryId=hair")]
    [InlineData("?isActive=maybe")]
    public async Task A_value_of_the_query_string_that_is_not_of_its_type_should_be_a_400_and_send_nothing(string query)
    {
        var dispatcher = Answering(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Get, Base + query);
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("code").GetString().ShouldBe("Http.BadRequest");
        dispatcher.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Listing_should_answer_the_page_with_the_category_and_the_price_as_a_number()
    {
        await using var app = await StartAsync(Answering(UserRole.Professional));

        var body = await TestApi.ReadAsync(await CallAsync(app, HttpMethod.Get, Base), _ct);

        body.GetProperty("page").GetInt32().ShouldBe(2);
        body.GetProperty("totalPages").GetInt32().ShouldBe(3);

        var item = body.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("id").GetGuid().ShouldBe(ServiceId);
        item.GetProperty("name").GetString().ShouldBe("Haircut");
        item.GetProperty("categoryId").GetGuid().ShouldBe(CategoryId);
        item.GetProperty("categoryName").GetString().ShouldBe("Hair");
        item.GetProperty("durationMinutes").GetInt32().ShouldBe(45);
        item.GetProperty("pricingType").GetString().ShouldBe("Fixed");
        item.GetProperty("basePrice").GetDecimal().ShouldBe(50m);
        item.GetProperty("isActive").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Getting_should_ask_for_the_service_of_the_route_and_answer_everything_it_has()
    {
        var dispatcher = Answering(UserRole.Professional);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Get, One);
        var body = await TestApi.ReadAsync(response, _ct);

        dispatcher.Requests.Single().ShouldBe(new GetServiceOfferingQuery(ServiceId));
        body.GetProperty("description").GetString().ShouldBe("Classic cut");
        body.GetProperty("durationMinutes").GetInt32().ShouldBe(45);
        body.GetProperty("pricingType").GetString().ShouldBe("Fixed");
        body.GetProperty("basePrice").GetDecimal().ShouldBe(50m);
        body.GetProperty("categoryId").GetGuid().ShouldBe(CategoryId);
    }

    [Fact]
    public async Task A_service_that_does_not_exist_should_be_a_404_with_its_code()
    {
        var dispatcher = StaffApi.As(UserRole.CompanyAdmin)
            .Reply<GetServiceOfferingQuery, ServiceOfferingDto>(_ => Error.NotFound("ServiceOffering.NotFound"));
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Get, One);
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.GetProperty("code").GetString().ShouldBe("ServiceOffering.NotFound");
    }

    // Create ------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Creating_should_send_the_body_as_the_command_and_answer_201_with_the_location()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "create");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location!.OriginalString.ShouldBe($"{Base}/{NewServiceId}");
        body.GetProperty("id").GetGuid().ShouldBe(NewServiceId);

        // 45.90 arrives as the decimal 45.90, not as the nearest binary fraction.
        dispatcher.Requests.Single().ShouldBe(new CreateServiceOfferingCommand(
            "Haircut", 45, new PricingData(PricingType.Fixed, 45.90m), "Classic cut", CategoryId));
    }

    [Fact]
    public async Task A_service_on_request_should_need_no_price_in_the_body()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(
            app,
            HttpMethod.Post,
            Base,
            """{ "name": "Coloring", "durationMinutes": 90, "pricing": { "type": "OnRequest" } }""");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        dispatcher.Requests.Single().ShouldBe(
            new CreateServiceOfferingCommand("Coloring", 90, new PricingData(PricingType.OnRequest)));
    }

    [Fact]
    public async Task A_type_of_price_written_as_a_number_should_be_a_400_and_send_nothing()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(
            app,
            HttpMethod.Post,
            Base,
            """{ "name": "Haircut", "durationMinutes": 45, "pricing": { "type": 0, "basePrice": 10 } }""");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("code").GetString().ShouldBe("Http.BadRequest");
        dispatcher.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("ServiceCategory.NotFound", HttpStatusCode.NotFound)]
    [InlineData("ServiceOffering.NameAlreadyExists", HttpStatusCode.Conflict)]
    public async Task A_refusal_of_the_use_case_should_be_its_status_with_its_code(string code, HttpStatusCode status)
    {
        var dispatcher = StaffApi.As(UserRole.CompanyAdmin).Reply<CreateServiceOfferingCommand, Guid>(
            _ => status == HttpStatusCode.NotFound ? Error.NotFound(code) : Error.Conflict(code));
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "create");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(status);
        body.GetProperty("code").GetString().ShouldBe(code);
    }

    // Update and pricing ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Updating_should_use_the_service_of_the_route_and_ignore_one_in_the_body()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "update");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(
            new UpdateServiceOfferingDetailsCommand(ServiceId, "Haircut Plus", 60, "Cut and beard", CategoryId));
    }

    [Fact]
    public async Task A_service_updated_with_no_category_in_the_body_should_leave_its_category()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Put, One, """{ "name": "Haircut", "durationMinutes": 45 }""");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(new UpdateServiceOfferingDetailsCommand(ServiceId, "Haircut", 45, null, null));
    }

    [Fact]
    public async Task Changing_the_pricing_should_send_the_body_as_the_pricing_of_the_service_of_the_route()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "change-pricing");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(
            new ChangeServiceOfferingPricingCommand(ServiceId, new PricingData(PricingType.Fixed, 60m)));
    }

    [Fact]
    public async Task Changing_to_on_request_should_send_no_price()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Put, $"{One}/pricing", """{ "type": "OnRequest" }""");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(
            new ChangeServiceOfferingPricingCommand(ServiceId, new PricingData(PricingType.OnRequest)));
    }

    [Theory]
    [InlineData("activate")]
    [InlineData("deactivate")]
    public async Task Activating_and_deactivating_should_send_the_service_of_the_route(string name)
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, name);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(
            name == "activate" ? new ActivateServiceOfferingCommand(ServiceId) : new DeactivateServiceOfferingCommand(ServiceId));
    }
}
