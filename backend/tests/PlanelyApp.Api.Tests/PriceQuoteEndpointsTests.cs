using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using PlanelyApp.Api.Endpoints.Catalog.Pricing;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.Pricing;
using PlanelyApp.Application.Catalog.Pricing.GetPrice;
using PlanelyApp.Application.Organization.Users.ResolveActor;
using PlanelyApp.Domain.Organization.Users;
using Shouldly;

namespace PlanelyApp.Api.Tests;

/// <summary>
/// The quote of a price: the services of an appointment at a branch, at a moment. It is a POST because the list of services is a body,
/// but it creates nothing and can be repeated, so it answers 200 with the quote. The branch is in the route, so the tenancy checks it
/// against the branches of the user (RN11). The instant is UTC (<c>2026-12-01T09:00:00Z</c>). The rules of the price are in the
/// Domain and in the use case, which have their own tests.
/// </summary>
public class PriceQuoteEndpointsTests
{
    private static readonly Guid BranchId = Guid.CreateVersion7();
    private static readonly Guid OtherBranch = Guid.CreateVersion7();
    private static readonly Guid ServiceId = Guid.CreateVersion7();
    private static readonly Guid OnRequestId = Guid.CreateVersion7();
    private static readonly Guid PromotionId = Guid.CreateVersion7();
    private static readonly Guid OtherCompany = Guid.CreateVersion7();

    private static readonly string Path = $"/companies/{StaffApi.Company}/branches/{BranchId}/price-quotes";

    private static readonly DateTime Start = new(2026, 12, 1, 9, 0, 0, DateTimeKind.Utc);

    // A service twice, and one on request: the lines follow the order asked.
    private static readonly string QuoteJson = $$"""
        { "startUtc": "2026-12-01T09:00:00Z", "serviceIds": ["{{ServiceId}}", "{{OnRequestId}}", "{{ServiceId}}"] }
        """;

    private static readonly PriceQuoteDto Quote = new(
        [
            new PriceLineDto(ServiceId, "Haircut", false, 50m, 5m, 45m, PromotionId, "Tuesday deal", false, true),
            new PriceLineDto(OnRequestId, "Coloring", true, null, 0m, null, null, null, false, true),
        ],
        50m,
        5m,
        45m,
        true);

    private static readonly UserRole[] CompanyRoles = [UserRole.CompanyAdmin, UserRole.Receptionist, UserRole.Professional];

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static FakeDispatcher WithReplies(FakeDispatcher dispatcher) =>
        dispatcher.Reply<GetPriceQuery, PriceQuoteDto>(_ => Quote);

    private static FakeDispatcher Answering(UserRole role) => WithReplies(StaffApi.As(role));

    private static FakeDispatcher Restricted(UserRole role) => WithReplies(new FakeDispatcher
    {
        Resolve = _ => new StaffActor(Guid.CreateVersion7(), StaffApi.Company, role, [BranchId]),
    });

    public static TheoryData<UserRole> EveryRole()
    {
        var data = new TheoryData<UserRole>();

        foreach (var role in Enum.GetValues<UserRole>())
        {
            data.Add(role);
        }

        return data;
    }

    private static Task<WebApplication> StartAsync(FakeDispatcher dispatcher) =>
        StaffApi.StartAsync(dispatcher, api => api.MapPriceQuotes());

    private Task<HttpResponseMessage> PostAsync(
        WebApplication app, string? path = null, string? json = null, bool signedIn = true) =>
        StaffApi.SendAsync(app, HttpMethod.Post, path ?? Path, _ct, StaffApi.Json(json ?? QuoteJson), signedIn);

    // Role and status ---------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(EveryRole))]
    public async Task The_staff_of_the_company_should_get_the_quote_and_nobody_else(UserRole role)
    {
        var dispatcher = Answering(role);
        await using var app = await StartAsync(dispatcher);

        var response = await PostAsync(app);

        if (CompanyRoles.Contains(role))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
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

    [Fact]
    public async Task Without_a_token_the_answer_should_be_401_and_nothing_should_be_sent()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await PostAsync(app, signedIn: false);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        dispatcher.Requests.ShouldBeEmpty();
        dispatcher.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_company_administrator_should_be_refused_in_another_company()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await PostAsync(app, $"/companies/{OtherCompany}/branches/{BranchId}/price-quotes");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        body.GetProperty("code").GetString().ShouldBe("Company.AccessDenied");
        dispatcher.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_user_restricted_to_a_branch_should_get_the_quote_of_that_branch()
    {
        var dispatcher = Restricted(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await PostAsync(app);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        dispatcher.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_branch_outside_the_list_of_the_user_should_be_refused_before_the_endpoint()
    {
        var dispatcher = Restricted(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await PostAsync(app, $"/companies/{StaffApi.Company}/branches/{OtherBranch}/price-quotes");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        body.GetProperty("code").GetString().ShouldBe("Branch.AccessDenied");
        dispatcher.Requests.ShouldBeEmpty();
    }

    // The quote ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Quoting_should_send_the_branch_of_the_route_the_instant_and_the_services_in_the_order_asked()
    {
        var dispatcher = Answering(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await PostAsync(app);

        // A POST that creates nothing: 200, and not 201.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var query = dispatcher.Requests.Single().ShouldBeOfType<GetPriceQuery>();
        query.BranchId.ShouldBe(BranchId);
        query.StartUtc.ShouldBe(Start);
        query.StartUtc.Kind.ShouldBe(DateTimeKind.Utc);
        query.ServiceIds.ShouldBe([ServiceId, OnRequestId, ServiceId]);
    }

    [Fact]
    public async Task A_branch_in_the_body_should_be_ignored_because_the_branch_is_the_one_of_the_route()
    {
        var dispatcher = Answering(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await PostAsync(
            app, json: $$"""{ "branchId": "{{OtherBranch}}", "startUtc": "2026-12-01T09:00:00Z", "serviceIds": ["{{ServiceId}}"] }""");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        dispatcher.Requests.Single().ShouldBeOfType<GetPriceQuery>().BranchId.ShouldBe(BranchId);
    }

    [Fact]
    public async Task The_quote_should_answer_the_lines_and_the_totals_with_no_price_for_a_service_on_request()
    {
        await using var app = await StartAsync(Answering(UserRole.Professional));

        var body = await TestApi.ReadAsync(await PostAsync(app), _ct);

        body.GetProperty("listTotal").GetDecimal().ShouldBe(50m);
        body.GetProperty("discountTotal").GetDecimal().ShouldBe(5m);
        body.GetProperty("finalTotal").GetDecimal().ShouldBe(45m);
        body.GetProperty("hasServiceOnRequest").GetBoolean().ShouldBeTrue();

        var lines = body.GetProperty("lines").EnumerateArray().ToList();
        lines.Count.ShouldBe(2);

        lines[0].GetProperty("serviceOfferingId").GetGuid().ShouldBe(ServiceId);
        lines[0].GetProperty("serviceName").GetString().ShouldBe("Haircut");
        lines[0].GetProperty("isOnRequest").GetBoolean().ShouldBeFalse();
        lines[0].GetProperty("listPrice").GetDecimal().ShouldBe(50m);
        lines[0].GetProperty("discount").GetDecimal().ShouldBe(5m);
        lines[0].GetProperty("finalPrice").GetDecimal().ShouldBe(45m);
        lines[0].GetProperty("promotionId").GetGuid().ShouldBe(PromotionId);
        lines[0].GetProperty("promotionName").GetString().ShouldBe("Tuesday deal");
        lines[0].GetProperty("isFromBundle").GetBoolean().ShouldBeFalse();
        lines[0].GetProperty("isAvailable").GetBoolean().ShouldBeTrue();

        lines[1].GetProperty("isOnRequest").GetBoolean().ShouldBeTrue();
        lines[1].GetProperty("listPrice").ValueKind.ShouldBe(JsonValueKind.Null);
        lines[1].GetProperty("finalPrice").ValueKind.ShouldBe(JsonValueKind.Null);
        lines[1].GetProperty("promotionId").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("Branch.NotFound")]
    [InlineData("ServiceOffering.NotFound")]
    public async Task A_branch_or_a_service_that_does_not_exist_should_be_a_404_with_its_code(string code)
    {
        var dispatcher = StaffApi.As(UserRole.CompanyAdmin)
            .Reply<GetPriceQuery, PriceQuoteDto>(_ => Error.NotFound(code));
        await using var app = await StartAsync(dispatcher);

        var response = await PostAsync(app);
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.GetProperty("code").GetString().ShouldBe(code);
    }

    [Theory]
    [InlineData("""{ "startUtc": "tomorrow", "serviceIds": [] }""")]
    [InlineData("""{ "startUtc": "2026-12-01T09:00:00Z", "serviceIds": ["not-a-guid"] }""")]
    public async Task A_body_that_is_not_of_its_types_should_be_a_400_and_send_nothing(string json)
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await PostAsync(app, json: json);
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("code").GetString().ShouldBe("Http.BadRequest");
        dispatcher.Requests.ShouldBeEmpty();
    }
}
