using System.Net;
using Microsoft.AspNetCore.Builder;
using PlanelyApp.Api.Endpoints.Catalog.BranchOfferings;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.BranchOfferings;
using PlanelyApp.Application.Catalog.BranchOfferings.ClearPrice;
using PlanelyApp.Application.Catalog.BranchOfferings.List;
using PlanelyApp.Application.Catalog.BranchOfferings.SetAvailability;
using PlanelyApp.Application.Catalog.BranchOfferings.SetPrice;
using PlanelyApp.Application.Organization.Users.ResolveActor;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Users;
using Shouldly;

namespace PlanelyApp.Api.Tests;

/// <summary>
/// The endpoints of the services as a branch has them (availability and price of its own). The branch is in the route, so the tenancy
/// checks it against the branches of the user (RN11) before the endpoint. The branch and the service of the route are the ones the
/// command gets, never ones from the body. A body that must say something (whether the service is available, the price) is a 400 when
/// it does not, because the default would mean something else: <c>false</c> takes the service off the branch, and <c>0</c> makes it
/// free. The rules of the branch offering are in the use cases.
/// </summary>
public class BranchOfferingEndpointsTests
{
    private static readonly Guid BranchId = Guid.CreateVersion7();
    private static readonly Guid OtherBranch = Guid.CreateVersion7();
    private static readonly Guid ServiceId = Guid.CreateVersion7();
    private static readonly Guid CategoryId = Guid.CreateVersion7();
    private static readonly Guid OtherCompany = Guid.CreateVersion7();

    private static readonly string Base = $"/companies/{StaffApi.Company}/branches/{BranchId}/service-offerings";
    private static readonly string One = $"{Base}/{ServiceId}";

    private const string AvailabilityJson = """{ "isAvailable": false }""";
    private const string PriceJson = """{ "price": 45.90 }""";

    private static readonly PagedResult<BranchOfferingDto> Page = new(
        [new BranchOfferingDto(ServiceId, "Haircut", CategoryId, "Hair", 45, PricingType.Fixed, 50m, 45.90m, 45.90m, true)], 2, 50, 101);

    // The services are what the whole staff offers and books; what each branch changes of them is for the administrator.
    private static readonly UserRole[] CompanyRoles = [UserRole.CompanyAdmin, UserRole.Receptionist, UserRole.Professional];
    private static readonly UserRole[] AdminOnly = [UserRole.CompanyAdmin];

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    /// <summary>Which roles each endpoint accepts, and what it answers when it works. The platform administrator has no branch (RN11).</summary>
    private static readonly Dictionary<string, (UserRole[] Roles, HttpStatusCode Success)> Rules = new()
    {
        ["list"] = (CompanyRoles, HttpStatusCode.OK),
        ["set-availability"] = (AdminOnly, HttpStatusCode.NoContent),
        ["set-price"] = (AdminOnly, HttpStatusCode.NoContent),
        ["clear-price"] = (AdminOnly, HttpStatusCode.NoContent),
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
        .Reply<ListBranchOfferingsQuery, PagedResult<BranchOfferingDto>>(_ => Page)
        .Reply<SetBranchOfferingAvailabilityCommand, Unit>(_ => Unit.Value)
        .Reply<SetBranchOfferingPriceCommand, Unit>(_ => Unit.Value)
        .Reply<ClearBranchOfferingPriceCommand, Unit>(_ => Unit.Value);

    private static FakeDispatcher Answering(UserRole role) => WithReplies(StaffApi.As(role));

    private static FakeDispatcher Restricted(UserRole role) => WithReplies(new FakeDispatcher
    {
        Resolve = _ => new StaffActor(Guid.CreateVersion7(), StaffApi.Company, role, [BranchId]),
    });

    private static (HttpMethod Method, string Path, string? Json) Route(string name, Guid? company = null, Guid? branch = null)
    {
        var root = $"/companies/{company ?? StaffApi.Company}/branches/{branch ?? BranchId}/service-offerings";
        var one = $"{root}/{ServiceId}";

        return name switch
        {
            "list" => (HttpMethod.Get, root, null),
            "set-availability" => (HttpMethod.Put, $"{one}/availability", AvailabilityJson),
            "set-price" => (HttpMethod.Put, $"{one}/price", PriceJson),
            "clear-price" => (HttpMethod.Delete, $"{one}/price", null),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };
    }

    private static Task<WebApplication> StartAsync(FakeDispatcher dispatcher) =>
        StaffApi.StartAsync(dispatcher, api => api.MapBranchOfferings());

    private Task<HttpResponseMessage> SendAsync(
        WebApplication app, string name, bool signedIn = true, Guid? company = null, Guid? branch = null)
    {
        var (method, path, json) = Route(name, company, branch);

        return StaffApi.SendAsync(app, method, path, _ct, json is null ? null : StaffApi.Json(json), signedIn);
    }

    private Task<HttpResponseMessage> CallAsync(
        WebApplication app, HttpMethod method, string path, string? json = null) =>
        StaffApi.SendAsync(app, method, path, _ct, json is null ? null : StaffApi.Json(json));

    private async Task AssertRequiredAsync(HttpResponseMessage response, string field, FakeDispatcher dispatcher)
    {
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("code").GetString().ShouldBe("Validation.Failed");
        var error = body.GetProperty("errors").EnumerateArray().Single();
        error.GetProperty("field").GetString().ShouldBe(field);
        error.GetProperty("code").GetString().ShouldBe("Validation.Required");
        dispatcher.Requests.ShouldBeEmpty();
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

            // The branch of the route is checked before the role, and the platform administrator has no branch.
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            new[] { "User.RoleNotAllowed", "Branch.AccessDenied" }.ShouldContain(body.GetProperty("code").GetString()!);
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

    // The branches of the user (RN11) -----------------------------------------------------------------------------

    [Fact]
    public async Task A_user_restricted_to_a_branch_should_list_the_services_of_that_branch()
    {
        var dispatcher = Restricted(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "list");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        dispatcher.Requests.Count.ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(EveryEndpoint))]
    public async Task A_branch_outside_the_list_of_the_user_should_be_refused_before_the_endpoint(string name)
    {
        var dispatcher = Restricted(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, name, branch: OtherBranch);
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        body.GetProperty("code").GetString().ShouldBe("Branch.AccessDenied");
        dispatcher.Requests.ShouldBeEmpty();
    }

    // List --------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("", null, null, 1, PageLimits.DefaultPageSize)]
    [InlineData("?search=hair&isAvailable=true&page=2&pageSize=50", "hair", true, 2, 50)]
    public async Task Listing_should_pass_the_branch_of_the_route_and_the_query_string_with_the_defaults_of_the_use_case(
        string query, string? search, bool? isAvailable, int page, int pageSize)
    {
        var dispatcher = Answering(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Get, Base + query);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        dispatcher.Requests.Single().ShouldBe(new ListBranchOfferingsQuery(BranchId, search, null, isAvailable, page, pageSize));
    }

    [Fact]
    public async Task Listing_should_pass_the_category_of_the_query_string()
    {
        var dispatcher = Answering(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Get, $"{Base}?categoryId={CategoryId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        dispatcher.Requests.Single().ShouldBe(new ListBranchOfferingsQuery(BranchId, CategoryId: CategoryId));
    }

    [Theory]
    [InlineData("?page=two")]
    [InlineData("?categoryId=hair")]
    [InlineData("?isAvailable=maybe")]
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
    public async Task Listing_should_answer_the_price_of_the_branch_next_to_the_base_price_and_the_availability()
    {
        await using var app = await StartAsync(Answering(UserRole.Professional));

        var body = await TestApi.ReadAsync(await CallAsync(app, HttpMethod.Get, Base), _ct);

        body.GetProperty("totalCount").GetInt32().ShouldBe(101);

        var item = body.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("serviceOfferingId").GetGuid().ShouldBe(ServiceId);
        item.GetProperty("categoryName").GetString().ShouldBe("Hair");
        item.GetProperty("pricingType").GetString().ShouldBe("Fixed");
        item.GetProperty("basePrice").GetDecimal().ShouldBe(50m);
        item.GetProperty("customPrice").GetDecimal().ShouldBe(45.90m);
        item.GetProperty("price").GetDecimal().ShouldBe(45.90m);
        item.GetProperty("isAvailable").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task A_branch_that_does_not_exist_should_be_a_404_with_its_code()
    {
        var dispatcher = StaffApi.As(UserRole.CompanyAdmin)
            .Reply<ListBranchOfferingsQuery, PagedResult<BranchOfferingDto>>(_ => Error.NotFound("Branch.NotFound"));
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "list");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.GetProperty("code").GetString().ShouldBe("Branch.NotFound");
    }

    // Availability ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_availability_should_be_what_the_body_says_for_the_branch_and_the_service_of_the_route(bool available)
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(
            app,
            HttpMethod.Put,
            $"{One}/availability",
            $$"""{ "branchId": "00000000-0000-0000-0000-000000000001", "serviceOfferingId": "00000000-0000-0000-0000-000000000002", "isAvailable": {{(available ? "true" : "false")}} }""");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(new SetBranchOfferingAvailabilityCommand(BranchId, ServiceId, available));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "isAvailable": null }""")]
    public async Task A_body_that_does_not_say_whether_the_service_is_available_should_be_a_400_and_not_take_it_off_the_branch(string json)
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Put, $"{One}/availability", json);

        await AssertRequiredAsync(response, "IsAvailable", dispatcher);
    }

    [Theory]
    [InlineData("set-availability")]
    [InlineData("set-price")]
    [InlineData("clear-price")]
    public async Task A_service_that_does_not_exist_should_be_a_404_with_its_code(string name)
    {
        var dispatcher = StaffApi.As(UserRole.CompanyAdmin)
            .Reply<SetBranchOfferingAvailabilityCommand, Unit>(_ => Error.NotFound("ServiceOffering.NotFound"))
            .Reply<SetBranchOfferingPriceCommand, Unit>(_ => Error.NotFound("ServiceOffering.NotFound"))
            .Reply<ClearBranchOfferingPriceCommand, Unit>(_ => Error.NotFound("ServiceOffering.NotFound"));
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, name);
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.GetProperty("code").GetString().ShouldBe("ServiceOffering.NotFound");
    }

    // Price -------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Setting_the_price_should_take_the_ids_from_the_route_and_the_price_from_the_body_as_a_decimal()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "set-price");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(new SetBranchOfferingPriceCommand(BranchId, ServiceId, 45.90m));
    }

    [Fact]
    public async Task A_price_of_zero_said_on_purpose_should_be_sent_as_zero()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Put, $"{One}/price", """{ "price": 0 }""");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(new SetBranchOfferingPriceCommand(BranchId, ServiceId, 0m));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "price": null }""")]
    public async Task A_body_with_no_price_should_be_a_400_and_not_make_the_service_free(string json)
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Put, $"{One}/price", json);

        await AssertRequiredAsync(response, "Price", dispatcher);
    }

    [Fact]
    public async Task Clearing_the_price_should_send_the_ids_of_the_route()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "clear-price");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(new ClearBranchOfferingPriceCommand(BranchId, ServiceId));
    }
}
