using System.Net;
using Microsoft.AspNetCore.Builder;
using PlanelyApp.Api.Endpoints.Organization;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Companies;
using PlanelyApp.Application.Organization.Companies.Activate;
using PlanelyApp.Application.Organization.Companies.Deactivate;
using PlanelyApp.Application.Organization.Companies.Get;
using PlanelyApp.Application.Organization.Companies.Register;
using PlanelyApp.Application.Organization.Companies.UpdateBookingPolicy;
using PlanelyApp.Application.Organization.Companies.UpdateDetails;
using PlanelyApp.Domain.Organization.Users;
using Shouldly;

namespace PlanelyApp.Api.Tests;

/// <summary>
/// The company endpoints do three things only: read the request, send the command or the query, and turn the result into HTTP.
/// The rules are in the use cases (their own tests); here the question is the route, the role, the shape of the body, the
/// status and the headers.
/// </summary>
public class CompanyEndpointsTests
{
    private static readonly Guid NewCompanyId = Guid.CreateVersion7();
    private static readonly Guid OtherCompany = Guid.CreateVersion7();

    private const string RegisterJson = """
        { "tradeName": "Studio Bela", "legalName": "Studio Bela Ltda", "cnpj": "11222333000181",
          "email": "contato@studiobela.com.br", "phone": "+5511999998888", "defaultLanguage": "pt-BR" }
        """;

    private const string UpdateJson = """
        { "tradeName": "Studio Nova", "legalName": "Studio Nova Ltda", "email": "oi@studionova.com.br",
          "phone": "+5511988887777", "defaultLanguage": "en" }
        """;

    private const string PolicyJson = """
        { "minimumNoticeHours": 2, "maximumWindowDays": 60, "cancellationDeadlineHours": 12,
          "intervalBetweenAppointmentsMinutes": 10, "travelTimeBetweenBranchesMinutes": 30, "requiresConfirmation": true }
        """;

    private static readonly BookingPolicyData Policy = new(2, 60, 12, 10, 30, true);

    private static readonly CompanyDto Company = new(
        StaffApi.Company, "Studio Bela", "Studio Bela Ltda", "11222333000181", "contato@studiobela.com.br",
        "+5511999998888", "pt-BR", Policy, true);

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    /// <summary>Which roles each endpoint accepts, and what it answers when it works.</summary>
    private static readonly Dictionary<string, (UserRole[] Roles, HttpStatusCode Success)> Rules = new()
    {
        ["register"] = ([UserRole.PlatformAdmin], HttpStatusCode.Created),
        ["get"] = (Enum.GetValues<UserRole>(), HttpStatusCode.OK),
        ["update"] = ([UserRole.CompanyAdmin], HttpStatusCode.NoContent),
        ["policy"] = ([UserRole.CompanyAdmin], HttpStatusCode.NoContent),
        ["activate"] = ([UserRole.PlatformAdmin], HttpStatusCode.NoContent),
        ["deactivate"] = ([UserRole.PlatformAdmin], HttpStatusCode.NoContent),
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

    private static FakeDispatcher Answering(UserRole role, Guid? company = null) => StaffApi.As(role, company)
        .Reply<RegisterCompanyCommand, Guid>(_ => NewCompanyId)
        .Reply<GetCompanyQuery, CompanyDto>(_ => Company)
        .Reply<UpdateCompanyDetailsCommand, Unit>(_ => Unit.Value)
        .Reply<UpdateBookingPolicyCommand, Unit>(_ => Unit.Value)
        .Reply<ActivateCompanyCommand, Unit>(_ => Unit.Value)
        .Reply<DeactivateCompanyCommand, Unit>(_ => Unit.Value);

    private static (HttpMethod Method, string Path, string? Json) Route(string name, Guid? company = null)
    {
        var path = $"/companies/{company ?? StaffApi.Company}";

        return name switch
        {
            "register" => (HttpMethod.Post, "/companies", RegisterJson),
            "get" => (HttpMethod.Get, path, null),
            "update" => (HttpMethod.Put, path, UpdateJson),
            "policy" => (HttpMethod.Put, $"{path}/booking-policy", PolicyJson),
            "activate" => (HttpMethod.Post, $"{path}/activate", null),
            "deactivate" => (HttpMethod.Post, $"{path}/deactivate", null),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };
    }

    private static Task<WebApplication> StartAsync(FakeDispatcher dispatcher) =>
        StaffApi.StartAsync(dispatcher, api => api.MapCompanies());

    private Task<HttpResponseMessage> SendAsync(
        WebApplication app, string name, bool signedIn = true, Guid? company = null, string? language = null)
    {
        var (method, path, json) = Route(name, company);

        return StaffApi.SendAsync(
            app, method, path, _ct, json is null ? null : StaffApi.Json(json), signedIn, language);
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
        var dispatcher = Answering(UserRole.PlatformAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, name, signedIn: false);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        dispatcher.Requests.ShouldBeEmpty();
        dispatcher.Asked.ShouldBeEmpty();
    }

    // Register ----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Registering_should_send_the_body_as_the_command_and_answer_201_with_the_location_and_the_id()
    {
        var dispatcher = Answering(UserRole.PlatformAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "register");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location!.OriginalString.ShouldBe($"/companies/{NewCompanyId}");
        body.GetProperty("id").GetGuid().ShouldBe(NewCompanyId);

        var command = dispatcher.Requests.Single().ShouldBeOfType<RegisterCompanyCommand>();
        command.TradeName.ShouldBe("Studio Bela");
        command.LegalName.ShouldBe("Studio Bela Ltda");
        command.Cnpj.ShouldBe("11222333000181");
        command.Email.ShouldBe("contato@studiobela.com.br");
        command.Phone.ShouldBe("+5511999998888");
        command.DefaultLanguage.ShouldBe("pt-BR");
        command.BookingPolicy.ShouldBeNull();
    }

    [Fact]
    public async Task Registering_may_carry_a_booking_policy()
    {
        var dispatcher = Answering(UserRole.PlatformAdmin);
        await using var app = await StartAsync(dispatcher);
        var json = RegisterJson.TrimEnd().TrimEnd('}') + $", \"bookingPolicy\": {PolicyJson} }}";

        var response = await StaffApi.SendAsync(app, HttpMethod.Post, "/companies", _ct, StaffApi.Json(json));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        dispatcher.Requests.Single().ShouldBeOfType<RegisterCompanyCommand>().BookingPolicy.ShouldBe(Policy);
    }

    [Fact]
    public async Task A_cnpj_that_is_already_registered_should_be_a_409_with_its_code()
    {
        var dispatcher = StaffApi.As(UserRole.PlatformAdmin)
            .Reply<RegisterCompanyCommand, Guid>(_ => Error.Conflict("Company.CnpjAlreadyRegistered"));
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "register");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Headers.Location.ShouldBeNull();
        body.GetProperty("code").GetString().ShouldBe("Company.CnpjAlreadyRegistered");
        body.GetProperty("detail").GetString().ShouldBe("Já existe uma empresa cadastrada com este CNPJ.");
    }

    [Fact]
    public async Task Registering_should_not_look_at_a_company_of_the_route()
    {
        // The platform administrator has no company: the route of this endpoint has none either.
        var dispatcher = Answering(UserRole.PlatformAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "register");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    // Get ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Getting_should_answer_200_with_the_company_in_camel_case()
    {
        var dispatcher = Answering(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "get");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        dispatcher.Requests.Single().ShouldBeOfType<GetCompanyQuery>();
        body.GetProperty("id").GetGuid().ShouldBe(StaffApi.Company);
        body.GetProperty("tradeName").GetString().ShouldBe("Studio Bela");
        body.GetProperty("cnpj").GetString().ShouldBe("11222333000181");
        body.GetProperty("defaultLanguage").GetString().ShouldBe("pt-BR");
        body.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        body.GetProperty("bookingPolicy").GetProperty("minimumNoticeHours").GetInt32().ShouldBe(2);
        body.GetProperty("bookingPolicy").GetProperty("requiresConfirmation").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task A_company_that_does_not_exist_should_be_a_404_with_the_text_of_the_language()
    {
        var dispatcher = StaffApi.As(UserRole.PlatformAdmin)
            .Reply<GetCompanyQuery, CompanyDto>(_ => Error.NotFound("Company.NotFound"));
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "get", company: OtherCompany, language: "en");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.GetProperty("code").GetString().ShouldBe("Company.NotFound");
    }

    [Theory]
    [InlineData("get")]
    [InlineData("update")]
    [InlineData("policy")]
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

    [Theory]
    [InlineData("get")]
    [InlineData("activate")]
    [InlineData("deactivate")]
    public async Task The_platform_administrator_should_work_in_any_company(string name)
    {
        var dispatcher = Answering(UserRole.PlatformAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, name, company: OtherCompany);

        response.IsSuccessStatusCode.ShouldBeTrue();
        dispatcher.Requests.Count.ShouldBe(1);
    }

    // Update ------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Updating_should_send_the_body_as_the_command_and_answer_204()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "update");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var command = dispatcher.Requests.Single().ShouldBeOfType<UpdateCompanyDetailsCommand>();
        command.ShouldBe(new UpdateCompanyDetailsCommand(
            "Studio Nova", "Studio Nova Ltda", "oi@studionova.com.br", "+5511988887777", "en"));
    }

    [Fact]
    public async Task Updating_with_a_broken_body_should_be_a_400_and_send_nothing()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await StaffApi.SendAsync(
            app, HttpMethod.Put, $"/companies/{StaffApi.Company}", _ct, StaffApi.Json("{ \"tradeName\": "));
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("code").GetString().ShouldBe("Http.BadRequest");
        dispatcher.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Updating_a_company_that_the_use_case_refuses_should_carry_its_status()
    {
        var dispatcher = StaffApi.As(UserRole.CompanyAdmin)
            .Reply<UpdateCompanyDetailsCommand, Unit>(_ => Error.NotFound("Company.NotFound"));
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "update");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // Booking policy ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_body_of_the_policy_should_be_the_policy_itself_and_not_a_command()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "policy");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBeOfType<UpdateBookingPolicyCommand>().Policy.ShouldBe(Policy);
    }

    // Activation --------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("activate")]
    [InlineData("deactivate")]
    public async Task Activating_and_deactivating_should_send_the_command_without_a_body(string name)
    {
        var dispatcher = Answering(UserRole.PlatformAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, name);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().GetType().ShouldBe(name == "activate" ? typeof(ActivateCompanyCommand) : typeof(DeactivateCompanyCommand));
    }

    // The route ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("/companies/not-a-guid")]
    [InlineData("/companies/00000000-0000-0000-0000-000000000000")]
    public async Task A_company_in_the_route_that_is_not_an_id_should_be_a_404(string path)
    {
        var dispatcher = Answering(UserRole.PlatformAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await StaffApi.SendAsync(app, HttpMethod.Get, path, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        dispatcher.Requests.ShouldBeEmpty();
    }
}
