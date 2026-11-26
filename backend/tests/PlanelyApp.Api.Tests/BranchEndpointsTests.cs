using System.Net;
using Microsoft.AspNetCore.Builder;
using PlanelyApp.Api.Endpoints.Organization.Branches;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Branches;
using PlanelyApp.Application.Organization.Branches.Activate;
using PlanelyApp.Application.Organization.Branches.Create;
using PlanelyApp.Application.Organization.Branches.Deactivate;
using PlanelyApp.Application.Organization.Branches.Get;
using PlanelyApp.Application.Organization.Branches.List;
using PlanelyApp.Application.Organization.Branches.RemoveBusinessHours;
using PlanelyApp.Application.Organization.Branches.SetBusinessHours;
using PlanelyApp.Application.Organization.Branches.UpdateDetails;
using PlanelyApp.Application.Organization.Shared;
using PlanelyApp.Application.Organization.Users.ResolveActor;
using PlanelyApp.Domain.Organization.Users;
using Shouldly;

namespace PlanelyApp.Api.Tests;

/// <summary>
/// The branch endpoints read the route and the body, send one command or query and turn the result into HTTP. The id of the branch
/// is the one of the route and never the one of the body. The rules of the branch are in the use cases, which have their own tests.
/// </summary>
public class BranchEndpointsTests
{
    private static readonly Guid BranchId = Guid.CreateVersion7();
    private static readonly Guid OtherBranch = Guid.CreateVersion7();
    private static readonly Guid OtherCompany = Guid.CreateVersion7();
    private static readonly Guid NewBranchId = Guid.CreateVersion7();

    private static readonly string Base = $"/companies/{StaffApi.Company}/branches";

    private static readonly AddressData Address = new("Rua das Flores", "123", null, "Centro", "Sao Paulo", "SP", "01310100");

    private const string AddressJson = """
        { "street": "Rua das Flores", "number": "123", "complement": null, "district": "Centro",
          "city": "Sao Paulo", "state": "SP", "postalCode": "01310100" }
        """;

    private static readonly string CreateJson =
        "{ \"name\": \"Centro\", \"address\": " + AddressJson + ", \"phone\": \"+551133334444\", \"timeZoneId\": \"America/Sao_Paulo\" }";

    private static readonly string UpdateJson =
        "{ \"branchId\": \"" + OtherBranch + "\", \"name\": \"Centro Novo\", \"address\": " + AddressJson
        + ", \"phone\": \"+551155556666\", \"timeZoneId\": \"America/Sao_Paulo\" }";

    private const string HoursJson = """
        { "start": "09:00:00", "end": "18:00:00", "breaks": [ { "start": "12:00:00", "end": "13:00:00", "description": "Lunch" } ] }
        """;

    private static readonly IReadOnlyList<BranchSummaryDto> Summaries =
        [new(BranchId, "Centro", "Sao Paulo", "SP", "+551133334444", true)];

    private static readonly BranchDto Branch = new(
        BranchId,
        "Centro",
        Address,
        "+551133334444",
        "America/Sao_Paulo",
        true,
        [new DailyScheduleData(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(18, 0), [new BreakData(new TimeOnly(12, 0), new TimeOnly(13, 0), "Lunch")])]);

    private static readonly UserRole[] CompanyRoles = [UserRole.CompanyAdmin, UserRole.Receptionist, UserRole.Professional];
    private static readonly UserRole[] AdminOnly = [UserRole.CompanyAdmin];

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    /// <summary>Which roles each endpoint accepts, and what it answers when it works. The platform administrator has no branch, so it is in none.</summary>
    private static readonly Dictionary<string, (UserRole[] Roles, HttpStatusCode Success)> Rules = new()
    {
        ["list"] = (CompanyRoles, HttpStatusCode.OK),
        ["get"] = (CompanyRoles, HttpStatusCode.OK),
        ["create"] = (AdminOnly, HttpStatusCode.Created),
        ["update"] = (AdminOnly, HttpStatusCode.NoContent),
        ["activate"] = (AdminOnly, HttpStatusCode.NoContent),
        ["deactivate"] = (AdminOnly, HttpStatusCode.NoContent),
        ["set-hours"] = (AdminOnly, HttpStatusCode.NoContent),
        ["remove-hours"] = (AdminOnly, HttpStatusCode.NoContent),
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
        .Reply<ListBranchesQuery, IReadOnlyList<BranchSummaryDto>>(_ => Result<IReadOnlyList<BranchSummaryDto>>.Success(Summaries))
        .Reply<GetBranchQuery, BranchDto>(_ => Branch)
        .Reply<CreateBranchCommand, Guid>(_ => NewBranchId)
        .Reply<UpdateBranchDetailsCommand, Unit>(_ => Unit.Value)
        .Reply<ActivateBranchCommand, Unit>(_ => Unit.Value)
        .Reply<DeactivateBranchCommand, Unit>(_ => Unit.Value)
        .Reply<SetBranchBusinessHoursCommand, Unit>(_ => Unit.Value)
        .Reply<RemoveBranchBusinessHoursCommand, Unit>(_ => Unit.Value);

    private static FakeDispatcher Answering(UserRole role, Guid? company = null) => WithReplies(StaffApi.As(role, company));

    private static (HttpMethod Method, string Path, string? Json) Route(string name, Guid? company = null)
    {
        var root = $"/companies/{company ?? StaffApi.Company}/branches";
        var branch = $"{root}/{BranchId}";

        return name switch
        {
            "list" => (HttpMethod.Get, root, null),
            "create" => (HttpMethod.Post, root, CreateJson),
            "get" => (HttpMethod.Get, branch, null),
            "update" => (HttpMethod.Put, branch, UpdateJson),
            "activate" => (HttpMethod.Post, $"{branch}/activate", null),
            "deactivate" => (HttpMethod.Post, $"{branch}/deactivate", null),
            "set-hours" => (HttpMethod.Put, $"{branch}/business-hours/Monday", HoursJson),
            "remove-hours" => (HttpMethod.Delete, $"{branch}/business-hours/Monday", null),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };
    }

    private static Task<WebApplication> StartAsync(FakeDispatcher dispatcher) =>
        StaffApi.StartAsync(dispatcher, api => api.MapBranches());

    private Task<HttpResponseMessage> SendAsync(WebApplication app, string name, bool signedIn = true, Guid? company = null)
    {
        var (method, path, json) = Route(name, company);

        return StaffApi.SendAsync(app, method, path, _ct, json is null ? null : StaffApi.Json(json), signedIn);
    }

    private Task<HttpResponseMessage> GetAsync(WebApplication app, string path) =>
        StaffApi.SendAsync(app, HttpMethod.Get, path, _ct);

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
    [InlineData("list")]
    [InlineData("create")]
    [InlineData("get")]
    [InlineData("update")]
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

    private static FakeDispatcher Restricted(UserRole role) => WithReplies(new FakeDispatcher
    {
        Resolve = _ => new StaffActor(Guid.CreateVersion7(), StaffApi.Company, role, [BranchId]),
    });

    [Fact]
    public async Task A_staff_member_should_work_in_a_branch_of_its_list()
    {
        var dispatcher = Restricted(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await GetAsync(app, $"{Base}/{BranchId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        dispatcher.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_staff_member_should_be_refused_in_a_branch_outside_its_list_even_to_change_it()
    {
        var dispatcher = Restricted(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var read = await GetAsync(app, $"{Base}/{OtherBranch}");
        var change = await StaffApi.SendAsync(
            app, HttpMethod.Post, $"{Base}/{OtherBranch}/deactivate", _ct);
        var body = await TestApi.ReadAsync(read, _ct);

        read.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        change.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        body.GetProperty("code").GetString().ShouldBe("Branch.AccessDenied");
        dispatcher.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_platform_administrator_should_have_no_branch_and_hear_that_before_anything_about_roles()
    {
        var dispatcher = Answering(UserRole.PlatformAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await GetAsync(app, $"{Base}/{BranchId}");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        body.GetProperty("code").GetString().ShouldBe("Branch.AccessDenied");
    }

    // List --------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("", null)]
    [InlineData("?isActive=true", true)]
    [InlineData("?isActive=false", false)]
    public async Task Listing_should_pass_the_active_filter_of_the_query_string(string query, bool? expected)
    {
        var dispatcher = Answering(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await GetAsync(app, Base + query);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        dispatcher.Requests.Single().ShouldBeOfType<ListBranchesQuery>().IsActive.ShouldBe(expected);
    }

    [Fact]
    public async Task A_filter_that_is_not_a_boolean_should_be_a_400_and_send_nothing()
    {
        var dispatcher = Answering(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await GetAsync(app, Base + "?isActive=maybe");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("code").GetString().ShouldBe("Http.BadRequest");
        dispatcher.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Listing_should_answer_an_array_in_camel_case()
    {
        await using var app = await StartAsync(Answering(UserRole.Professional));

        var body = await TestApi.ReadAsync(await GetAsync(app, Base), _ct);

        var item = body.EnumerateArray().Single();
        item.GetProperty("id").GetGuid().ShouldBe(BranchId);
        item.GetProperty("name").GetString().ShouldBe("Centro");
        item.GetProperty("city").GetString().ShouldBe("Sao Paulo");
        item.GetProperty("isActive").GetBoolean().ShouldBeTrue();
    }

    // Get ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Getting_should_ask_for_the_branch_of_the_route_and_answer_it_with_the_days_as_names()
    {
        var dispatcher = Answering(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await GetAsync(app, $"{Base}/{BranchId}");
        var body = await TestApi.ReadAsync(response, _ct);

        dispatcher.Requests.Single().ShouldBeOfType<GetBranchQuery>().BranchId.ShouldBe(BranchId);
        body.GetProperty("timeZoneId").GetString().ShouldBe("America/Sao_Paulo");
        body.GetProperty("address").GetProperty("postalCode").GetString().ShouldBe("01310100");
        var day = body.GetProperty("businessHours").EnumerateArray().Single();
        day.GetProperty("day").GetString().ShouldBe("Monday");
        day.GetProperty("start").GetString().ShouldBe("09:00:00");
        day.GetProperty("breaks").EnumerateArray().Single().GetProperty("description").GetString().ShouldBe("Lunch");
    }

    [Fact]
    public async Task A_branch_that_does_not_exist_should_be_a_404_with_its_code()
    {
        var dispatcher = StaffApi.As(UserRole.CompanyAdmin)
            .Reply<GetBranchQuery, BranchDto>(_ => Error.NotFound("Branch.NotFound"));
        await using var app = await StartAsync(dispatcher);

        var response = await GetAsync(app, $"{Base}/{BranchId}");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.GetProperty("code").GetString().ShouldBe("Branch.NotFound");
    }

    // Create ------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Creating_should_send_the_body_as_the_command_and_answer_201_with_the_location_of_the_branch()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "create");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location!.OriginalString.ShouldBe($"{Base}/{NewBranchId}");
        body.GetProperty("id").GetGuid().ShouldBe(NewBranchId);
        dispatcher.Requests.Single().ShouldBe(new CreateBranchCommand("Centro", Address, "+551133334444", "America/Sao_Paulo"));
    }

    [Fact]
    public async Task A_name_that_is_already_used_should_be_a_409_with_its_code()
    {
        var dispatcher = StaffApi.As(UserRole.CompanyAdmin)
            .Reply<CreateBranchCommand, Guid>(_ => Error.Conflict("Branch.NameAlreadyExists"));
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "create");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Headers.Location.ShouldBeNull();
        body.GetProperty("code").GetString().ShouldBe("Branch.NameAlreadyExists");
    }

    // Update ------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Updating_should_use_the_branch_of_the_route_and_ignore_one_in_the_body()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "update");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(new UpdateBranchDetailsCommand(
            BranchId, "Centro Novo", Address, "+551155556666", "America/Sao_Paulo"));
    }

    [Theory]
    [InlineData("activate")]
    [InlineData("deactivate")]
    public async Task Activating_and_deactivating_should_send_the_branch_of_the_route(string name)
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, name);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(
            name == "activate" ? new ActivateBranchCommand(BranchId) : new DeactivateBranchCommand(BranchId));
    }

    // Business hours ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task Setting_the_hours_should_take_the_day_from_the_route_and_the_rest_from_the_body()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "set-hours");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var command = dispatcher.Requests.Single().ShouldBeOfType<SetBranchBusinessHoursCommand>();
        command.BranchId.ShouldBe(BranchId);
        command.Schedule.Day.ShouldBe(DayOfWeek.Monday);
        command.Schedule.Start.ShouldBe(new TimeOnly(9, 0));
        command.Schedule.End.ShouldBe(new TimeOnly(18, 0));
        command.Schedule.Breaks.ShouldBe([new BreakData(new TimeOnly(12, 0), new TimeOnly(13, 0), "Lunch")]);
    }

    [Fact]
    public async Task A_day_with_no_breaks_in_the_body_should_have_an_empty_list_of_breaks()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await StaffApi.SendAsync(
            app,
            HttpMethod.Put,
            $"{Base}/{BranchId}/business-hours/Tuesday",
            _ct,
            StaffApi.Json("{ \"start\": \"08:00:00\", \"end\": \"12:00:00\" }"));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var schedule = dispatcher.Requests.Single().ShouldBeOfType<SetBranchBusinessHoursCommand>().Schedule;
        schedule.Day.ShouldBe(DayOfWeek.Tuesday);
        schedule.Breaks.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_day_that_is_not_a_name_of_a_weekday_should_be_a_400_problem_and_send_nothing()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await StaffApi.SendAsync(
            app, HttpMethod.Put, $"{Base}/{BranchId}/business-hours/Funday", _ct, StaffApi.Json(HoursJson));
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("code").GetString().ShouldBe("Http.BadRequest");
        dispatcher.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(HttpVerb.Put)]
    [InlineData(HttpVerb.Delete)]
    public async Task A_number_outside_the_weekdays_should_be_a_validation_error_on_the_day(HttpVerb verb)
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await StaffApi.SendAsync(
            app,
            verb == HttpVerb.Put ? HttpMethod.Put : HttpMethod.Delete,
            $"{Base}/{BranchId}/business-hours/9",
            _ct,
            verb == HttpVerb.Put ? StaffApi.Json(HoursJson) : null);
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("code").GetString().ShouldBe("Validation.Failed");
        body.GetProperty("errors").EnumerateArray().Single().GetProperty("field").GetString().ShouldBe("Day");
        dispatcher.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Closing_a_day_should_send_the_branch_and_the_day_of_the_route()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "remove-hours");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(new RemoveBranchBusinessHoursCommand(BranchId, DayOfWeek.Monday));
    }

    public enum HttpVerb
    {
        Put,
        Delete,
    }
}
