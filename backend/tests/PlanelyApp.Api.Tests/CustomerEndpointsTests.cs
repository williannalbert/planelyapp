using System.Net;
using Microsoft.AspNetCore.Builder;
using PlanelyApp.Api.Endpoints.Organization.Customers;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Customers;
using PlanelyApp.Application.Organization.Customers.ChangeMarketingConsent;
using PlanelyApp.Application.Organization.Customers.Get;
using PlanelyApp.Application.Organization.Customers.List;
using PlanelyApp.Application.Organization.Customers.Register;
using PlanelyApp.Application.Organization.Customers.UpdateContact;
using PlanelyApp.Application.Organization.Customers.UpdateProfile;
using PlanelyApp.Domain.Organization.Users;
using Shouldly;

namespace PlanelyApp.Api.Tests;

/// <summary>
/// The customer endpoints read the route, the query string and the body, send one command or query and turn the result into HTTP.
/// The id of the route is the one the command gets, never one from the body. A field the body must say (the marketing consent) is a 400
/// when it is left out, because a missing boolean would otherwise arrive as "false" and withdraw a consent nobody withdrew.
/// The rules of the customer are in the use cases, which have their own tests.
/// </summary>
public class CustomerEndpointsTests
{
    private static readonly Guid CustomerId = Guid.CreateVersion7();
    private static readonly Guid NewCustomerId = Guid.CreateVersion7();
    private static readonly Guid OtherCompany = Guid.CreateVersion7();

    private static readonly string Base = $"/companies/{StaffApi.Company}/customers";
    private static readonly string One = $"{Base}/{CustomerId}";

    private static readonly DateTime LastAccess = new(2026, 11, 20, 14, 30, 0, DateTimeKind.Utc);

    private const string RegisterJson = """{ "name": "Ana Souza", "email": "ana@example.com", "phone": "+5511999990000", "acceptsMarketing": true, "preferredLanguage": "pt-BR" }""";
    private const string ContactJson = """{ "customerId": "00000000-0000-0000-0000-000000000001", "email": "ana.souza@example.com", "phone": "+5511988887777" }""";
    private const string ProfileJson = """{ "customerId": "00000000-0000-0000-0000-000000000001", "name": "Ana Lima", "cpf": "52998224725", "birthDate": "1990-05-17", "preferredLanguage": "en" }""";
    private const string ConsentJson = """{ "acceptsMarketing": true }""";

    private static readonly PagedResult<CustomerSummaryDto> Page = new(
        [new CustomerSummaryDto(CustomerId, "Ana Souza", "ana@example.com", "+5511999990000", true, LastAccess)], 2, 50, 101);

    private static readonly CustomerDto Customer = new(
        CustomerId, "Ana Souza", "ana@example.com", "+5511999990000", "529.982.247-25", new DateOnly(1990, 5, 17), LastAccess, true, "pt-BR");

    // Personal data (CPF, birth date, contacts): the counter and the administration. A professional sees the customer inside an
    // appointment, not in the register.
    private static readonly UserRole[] Counter = [UserRole.CompanyAdmin, UserRole.Receptionist];

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    /// <summary>Which roles each endpoint accepts, and what it answers when it works.</summary>
    private static readonly Dictionary<string, (UserRole[] Roles, HttpStatusCode Success)> Rules = new()
    {
        ["list"] = (Counter, HttpStatusCode.OK),
        ["create"] = (Counter, HttpStatusCode.Created),
        ["get"] = (Counter, HttpStatusCode.OK),
        ["update-contact"] = (Counter, HttpStatusCode.NoContent),
        ["update-profile"] = (Counter, HttpStatusCode.NoContent),
        ["change-consent"] = (Counter, HttpStatusCode.NoContent),
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
        .Reply<ListCustomersQuery, PagedResult<CustomerSummaryDto>>(_ => Page)
        .Reply<GetCustomerQuery, CustomerDto>(_ => Customer)
        .Reply<RegisterCustomerCommand, Guid>(_ => NewCustomerId)
        .Reply<UpdateCustomerContactCommand, Unit>(_ => Unit.Value)
        .Reply<UpdateCustomerProfileCommand, Unit>(_ => Unit.Value)
        .Reply<ChangeCustomerMarketingConsentCommand, Unit>(_ => Unit.Value);

    private static FakeDispatcher Answering(UserRole role) => WithReplies(StaffApi.As(role));

    private static (HttpMethod Method, string Path, string? Json) Route(string name, Guid? company = null)
    {
        var root = $"/companies/{company ?? StaffApi.Company}/customers";
        var one = $"{root}/{CustomerId}";

        return name switch
        {
            "list" => (HttpMethod.Get, root, null),
            "create" => (HttpMethod.Post, root, RegisterJson),
            "get" => (HttpMethod.Get, one, null),
            "update-contact" => (HttpMethod.Put, $"{one}/contact", ContactJson),
            "update-profile" => (HttpMethod.Put, $"{one}/profile", ProfileJson),
            "change-consent" => (HttpMethod.Put, $"{one}/marketing-consent", ConsentJson),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };
    }

    private static Task<WebApplication> StartAsync(FakeDispatcher dispatcher) =>
        StaffApi.StartAsync(dispatcher, api => api.MapCustomers());

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
    [InlineData("", null, 1, PageLimits.DefaultPageSize)]
    [InlineData("?search=ana&page=2&pageSize=50", "ana", 2, 50)]
    [InlineData("?search=%2B5511999990000", "+5511999990000", 1, PageLimits.DefaultPageSize)]
    public async Task Listing_should_pass_the_search_and_the_page_of_the_query_string_with_the_defaults_of_the_use_case(
        string query, string? search, int page, int pageSize)
    {
        var dispatcher = Answering(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Get, Base + query);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        dispatcher.Requests.Single().ShouldBe(new ListCustomersQuery(search, page, pageSize));
    }

    [Fact]
    public async Task A_page_that_is_not_a_number_should_be_a_400_and_send_nothing()
    {
        var dispatcher = Answering(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Get, Base + "?page=two");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("code").GetString().ShouldBe("Http.BadRequest");
        dispatcher.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Listing_should_answer_the_page_in_camel_case_and_without_the_personal_data_a_list_does_not_need()
    {
        await using var app = await StartAsync(Answering(UserRole.CompanyAdmin));

        var body = await TestApi.ReadAsync(await CallAsync(app, HttpMethod.Get, Base), _ct);

        body.GetProperty("page").GetInt32().ShouldBe(2);
        body.GetProperty("pageSize").GetInt32().ShouldBe(50);
        body.GetProperty("totalCount").GetInt32().ShouldBe(101);
        body.GetProperty("totalPages").GetInt32().ShouldBe(3);

        var item = body.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("id").GetGuid().ShouldBe(CustomerId);
        item.GetProperty("name").GetString().ShouldBe("Ana Souza");
        item.GetProperty("email").GetString().ShouldBe("ana@example.com");
        item.GetProperty("phone").GetString().ShouldBe("+5511999990000");
        item.GetProperty("acceptsMarketing").GetBoolean().ShouldBeTrue();
        item.GetProperty("lastAccessAt").GetString().ShouldBe("2026-11-20T14:30:00Z");
        item.TryGetProperty("cpf", out _).ShouldBeFalse();
        item.TryGetProperty("birthDate", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Getting_should_ask_for_the_customer_of_the_route_and_answer_everything_it_has()
    {
        var dispatcher = Answering(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Get, One);
        var body = await TestApi.ReadAsync(response, _ct);

        dispatcher.Requests.Single().ShouldBe(new GetCustomerQuery(CustomerId));
        body.GetProperty("id").GetGuid().ShouldBe(CustomerId);
        body.GetProperty("cpf").GetString().ShouldBe("529.982.247-25");
        body.GetProperty("birthDate").GetString().ShouldBe("1990-05-17");
        body.GetProperty("lastAccessAt").GetString().ShouldBe("2026-11-20T14:30:00Z");
        body.GetProperty("acceptsMarketing").GetBoolean().ShouldBeTrue();
        body.GetProperty("preferredLanguage").GetString().ShouldBe("pt-BR");
    }

    [Fact]
    public async Task A_customer_that_does_not_exist_should_be_a_404_with_its_code()
    {
        var dispatcher = StaffApi.As(UserRole.CompanyAdmin)
            .Reply<GetCustomerQuery, CustomerDto>(_ => Error.NotFound("Customer.NotFound"));
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Get, One);
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.GetProperty("code").GetString().ShouldBe("Customer.NotFound");
    }

    // Register ----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Registering_should_send_the_body_as_the_command_and_answer_201_with_the_location()
    {
        var dispatcher = Answering(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "create");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location!.OriginalString.ShouldBe($"{Base}/{NewCustomerId}");
        body.GetProperty("id").GetGuid().ShouldBe(NewCustomerId);
        dispatcher.Requests.Single().ShouldBe(
            new RegisterCustomerCommand("Ana Souza", "ana@example.com", "+5511999990000", true, "pt-BR"));
    }

    [Fact]
    public async Task A_customer_with_only_the_contact_in_the_body_should_have_no_consent_and_nothing_else()
    {
        var dispatcher = Answering(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Post, Base, """{ "email": "ana@example.com" }""");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        dispatcher.Requests.Single().ShouldBe(new RegisterCustomerCommand(Email: "ana@example.com"));
    }

    [Fact]
    public async Task An_e_mail_that_is_already_registered_should_be_a_409_with_its_code()
    {
        var dispatcher = StaffApi.As(UserRole.Receptionist)
            .Reply<RegisterCustomerCommand, Guid>(_ => Error.Conflict("Customer.EmailAlreadyRegistered"));
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "create");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        body.GetProperty("code").GetString().ShouldBe("Customer.EmailAlreadyRegistered");
    }

    // Contact and profile -----------------------------------------------------------------------------------------

    [Fact]
    public async Task Updating_the_contact_should_use_the_customer_of_the_route_and_ignore_one_in_the_body()
    {
        var dispatcher = Answering(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "update-contact");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(
            new UpdateCustomerContactCommand(CustomerId, "ana.souza@example.com", "+5511988887777"));
    }

    [Fact]
    public async Task A_contact_left_out_of_the_body_should_arrive_as_none_so_the_use_case_removes_it()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Put, $"{One}/contact", """{ "email": "ana@example.com" }""");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(new UpdateCustomerContactCommand(CustomerId, "ana@example.com", null));
    }

    [Fact]
    public async Task Updating_the_profile_should_use_the_customer_of_the_route_and_ignore_one_in_the_body()
    {
        var dispatcher = Answering(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "update-profile");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(
            new UpdateCustomerProfileCommand(CustomerId, "Ana Lima", "52998224725", new DateOnly(1990, 5, 17), "en"));
    }

    [Fact]
    public async Task A_profile_with_fields_left_out_should_arrive_with_none_of_them_so_the_use_case_clears_them()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Put, $"{One}/profile", """{ "name": "Ana Lima" }""");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(new UpdateCustomerProfileCommand(CustomerId, "Ana Lima", null, null, null));
    }

    [Fact]
    public async Task A_birth_date_that_is_not_written_as_a_date_should_be_a_400_and_send_nothing()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Put, $"{One}/profile", """{ "name": "Ana", "birthDate": "17/05/1990" }""");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("code").GetString().ShouldBe("Http.BadRequest");
        dispatcher.Requests.ShouldBeEmpty();
    }

    // Marketing consent -------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_consent_should_be_given_or_withdrawn_as_the_body_says(bool accepts)
    {
        var dispatcher = Answering(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(
            app, HttpMethod.Put, $"{One}/marketing-consent", $$"""{ "acceptsMarketing": {{(accepts ? "true" : "false")}} }""");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(new ChangeCustomerMarketingConsentCommand(CustomerId, accepts));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "acceptsMarketing": null }""")]
    public async Task A_body_that_does_not_say_the_consent_should_be_a_400_on_the_field_and_not_a_withdrawal(string json)
    {
        var dispatcher = Answering(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Put, $"{One}/marketing-consent", json);
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("code").GetString().ShouldBe("Validation.Failed");
        var error = body.GetProperty("errors").EnumerateArray().Single();
        error.GetProperty("field").GetString().ShouldBe("AcceptsMarketing");
        error.GetProperty("code").GetString().ShouldBe("Validation.Required");
        dispatcher.Requests.ShouldBeEmpty();
    }
}
