using System.Net;
using Microsoft.AspNetCore.Builder;
using PlanelyApp.Api.Endpoints.Organization.Professionals;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Professionals;
using PlanelyApp.Application.Organization.Professionals.Activate;
using PlanelyApp.Application.Organization.Professionals.AddTimeOff;
using PlanelyApp.Application.Organization.Professionals.Deactivate;
using PlanelyApp.Application.Organization.Professionals.DisableOffering;
using PlanelyApp.Application.Organization.Professionals.EnableOffering;
using PlanelyApp.Application.Organization.Professionals.Get;
using PlanelyApp.Application.Organization.Professionals.LinkUser;
using PlanelyApp.Application.Organization.Professionals.List;
using PlanelyApp.Application.Organization.Professionals.Register;
using PlanelyApp.Application.Organization.Professionals.RemoveTimeOff;
using PlanelyApp.Application.Organization.Professionals.RemoveWorkingHours;
using PlanelyApp.Application.Organization.Professionals.SetWorkingHours;
using PlanelyApp.Application.Organization.Professionals.UnlinkUser;
using PlanelyApp.Application.Organization.Professionals.UpdateDetails;
using PlanelyApp.Application.Organization.Shared;
using PlanelyApp.Application.Organization.Users.ResolveActor;
using PlanelyApp.Domain.Organization.Users;
using Shouldly;

namespace PlanelyApp.Api.Tests;

/// <summary>
/// The professional endpoints read the route and the body, send one command or query and turn the result into HTTP. The ids of the
/// route (professional, branch, day, time off, service) are the ones the command gets, never one from the body. A route with a
/// <c>branchId</c> is checked against the branches of the user before the endpoint (RN11), so the working hours at a branch need no
/// check of their own. The rules of the professional are in the use cases, which have their own tests.
/// </summary>
public class ProfessionalEndpointsTests
{
    private static readonly Guid ProfessionalId = Guid.CreateVersion7();
    private static readonly Guid NewProfessionalId = Guid.CreateVersion7();
    private static readonly Guid BranchId = Guid.CreateVersion7();
    private static readonly Guid OtherBranch = Guid.CreateVersion7();
    private static readonly Guid OtherCompany = Guid.CreateVersion7();
    private static readonly Guid TimeOffId = Guid.CreateVersion7();
    private static readonly Guid ServiceId = Guid.CreateVersion7();
    private static readonly Guid UserId = Guid.CreateVersion7();

    private static readonly string Base = $"/companies/{StaffApi.Company}/professionals";
    private static readonly string One = $"{Base}/{ProfessionalId}";

    private static readonly DateTime Start = new(2026, 12, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc);

    private const string RegisterJson = """{ "name": "Ana Souza", "specialty": "Barber" }""";
    private const string UpdateJson = """{ "professionalId": "00000000-0000-0000-0000-000000000001", "name": "Ana Lima", "specialty": "Colorist" }""";
    private const string HoursJson = """
        { "start": "09:00:00", "end": "18:00:00", "breaks": [ { "start": "12:00:00", "end": "13:00:00", "description": "Lunch" } ] }
        """;
    private const string TimeOffJson = """{ "startUtc": "2026-12-01T09:00:00Z", "endUtc": "2026-12-01T12:00:00Z", "reason": "Dentist" }""";

    private static readonly IReadOnlyList<ProfessionalSummaryDto> Summaries = [new(ProfessionalId, "Ana Souza", "Barber", true)];

    private static readonly ProfessionalDto Professional = new(
        ProfessionalId,
        "Ana Souza",
        "Barber",
        UserId,
        true,
        [new WorkingHoursData(BranchId, new DailyScheduleData(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(18, 0), []))],
        [new TimeOffData(TimeOffId, Start, End, "Dentist")],
        [new OfferingData(ServiceId, 45)]);

    private static readonly UserRole[] CompanyRoles = [UserRole.CompanyAdmin, UserRole.Receptionist, UserRole.Professional];
    private static readonly UserRole[] AdminAndReceptionist = [UserRole.CompanyAdmin, UserRole.Receptionist];
    private static readonly UserRole[] AdminOnly = [UserRole.CompanyAdmin];

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    /// <summary>Which roles each endpoint accepts, and what it answers when it works. The platform administrator is in none (no branch, RN11).</summary>
    private static readonly Dictionary<string, (UserRole[] Roles, HttpStatusCode Success)> Rules = new()
    {
        ["list"] = (CompanyRoles, HttpStatusCode.OK),
        ["get"] = (CompanyRoles, HttpStatusCode.OK),
        ["create"] = (AdminOnly, HttpStatusCode.Created),
        ["update"] = (AdminOnly, HttpStatusCode.NoContent),
        ["activate"] = (AdminOnly, HttpStatusCode.NoContent),
        ["deactivate"] = (AdminOnly, HttpStatusCode.NoContent),
        ["set-hours"] = (AdminAndReceptionist, HttpStatusCode.NoContent),
        ["remove-hours"] = (AdminAndReceptionist, HttpStatusCode.NoContent),
        ["add-time-off"] = (AdminAndReceptionist, HttpStatusCode.Created),
        ["remove-time-off"] = (AdminAndReceptionist, HttpStatusCode.NoContent),
        ["enable-offering"] = (AdminOnly, HttpStatusCode.NoContent),
        ["disable-offering"] = (AdminOnly, HttpStatusCode.NoContent),
        ["link-user"] = (AdminOnly, HttpStatusCode.NoContent),
        ["unlink-user"] = (AdminOnly, HttpStatusCode.NoContent),
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
        .Reply<ListProfessionalsQuery, IReadOnlyList<ProfessionalSummaryDto>>(
            _ => Result<IReadOnlyList<ProfessionalSummaryDto>>.Success(Summaries))
        .Reply<GetProfessionalQuery, ProfessionalDto>(_ => Professional)
        .Reply<RegisterProfessionalCommand, Guid>(_ => NewProfessionalId)
        .Reply<UpdateProfessionalDetailsCommand, Unit>(_ => Unit.Value)
        .Reply<ActivateProfessionalCommand, Unit>(_ => Unit.Value)
        .Reply<DeactivateProfessionalCommand, Unit>(_ => Unit.Value)
        .Reply<SetProfessionalWorkingHoursCommand, Unit>(_ => Unit.Value)
        .Reply<RemoveProfessionalWorkingHoursCommand, Unit>(_ => Unit.Value)
        .Reply<AddProfessionalTimeOffCommand, Guid>(_ => TimeOffId)
        .Reply<RemoveProfessionalTimeOffCommand, Unit>(_ => Unit.Value)
        .Reply<EnableProfessionalOfferingCommand, Unit>(_ => Unit.Value)
        .Reply<DisableProfessionalOfferingCommand, Unit>(_ => Unit.Value)
        .Reply<LinkProfessionalUserCommand, Unit>(_ => Unit.Value)
        .Reply<UnlinkProfessionalUserCommand, Unit>(_ => Unit.Value);

    private static FakeDispatcher Answering(UserRole role, Guid? company = null) => WithReplies(StaffApi.As(role, company));

    private static (HttpMethod Method, string Path, string? Json) Route(string name, Guid? company = null)
    {
        var root = $"/companies/{company ?? StaffApi.Company}/professionals";
        var one = $"{root}/{ProfessionalId}";
        var hours = $"{one}/branches/{BranchId}/working-hours/Monday";

        return name switch
        {
            "list" => (HttpMethod.Get, root, null),
            "create" => (HttpMethod.Post, root, RegisterJson),
            "get" => (HttpMethod.Get, one, null),
            "update" => (HttpMethod.Put, one, UpdateJson),
            "activate" => (HttpMethod.Post, $"{one}/activate", null),
            "deactivate" => (HttpMethod.Post, $"{one}/deactivate", null),
            "set-hours" => (HttpMethod.Put, hours, HoursJson),
            "remove-hours" => (HttpMethod.Delete, hours, null),
            "add-time-off" => (HttpMethod.Post, $"{one}/time-offs", TimeOffJson),
            "remove-time-off" => (HttpMethod.Delete, $"{one}/time-offs/{TimeOffId}", null),
            "enable-offering" => (HttpMethod.Put, $"{one}/offerings/{ServiceId}", """{ "customDurationMinutes": 45 }"""),
            "disable-offering" => (HttpMethod.Delete, $"{one}/offerings/{ServiceId}", null),
            "link-user" => (HttpMethod.Put, $"{one}/user", $$"""{ "userId": "{{UserId}}" }"""),
            "unlink-user" => (HttpMethod.Delete, $"{one}/user", null),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };
    }

    private static Task<WebApplication> StartAsync(FakeDispatcher dispatcher) =>
        StaffApi.StartAsync(dispatcher, api => api.MapProfessionals());

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
    public async Task The_working_hours_should_be_allowed_at_a_branch_of_the_list()
    {
        var dispatcher = Restricted(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Put, $"{One}/branches/{BranchId}/working-hours/Monday", HoursJson);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task The_working_hours_should_be_refused_at_a_branch_outside_the_list_to_set_and_to_remove()
    {
        var dispatcher = Restricted(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var set = await CallAsync(app, HttpMethod.Put, $"{One}/branches/{OtherBranch}/working-hours/Monday", HoursJson);
        var remove = await CallAsync(app, HttpMethod.Delete, $"{One}/branches/{OtherBranch}/working-hours/Monday");
        var body = await TestApi.ReadAsync(set, _ct);

        set.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        remove.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        body.GetProperty("code").GetString().ShouldBe("Branch.AccessDenied");
        dispatcher.Requests.ShouldBeEmpty();
    }

    // List and get ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("", null)]
    [InlineData("?isActive=true", true)]
    [InlineData("?isActive=false", false)]
    public async Task Listing_should_pass_the_active_filter_of_the_query_string(string query, bool? expected)
    {
        var dispatcher = Answering(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Get, Base + query);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        dispatcher.Requests.Single().ShouldBeOfType<ListProfessionalsQuery>().IsActive.ShouldBe(expected);
    }

    [Fact]
    public async Task A_filter_that_is_not_a_boolean_should_be_a_400_and_send_nothing()
    {
        var dispatcher = Answering(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Get, Base + "?isActive=maybe");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("code").GetString().ShouldBe("Http.BadRequest");
        dispatcher.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Listing_should_answer_an_array_in_camel_case()
    {
        await using var app = await StartAsync(Answering(UserRole.Professional));

        var body = await TestApi.ReadAsync(await CallAsync(app, HttpMethod.Get, Base), _ct);

        var item = body.EnumerateArray().Single();
        item.GetProperty("id").GetGuid().ShouldBe(ProfessionalId);
        item.GetProperty("name").GetString().ShouldBe("Ana Souza");
        item.GetProperty("specialty").GetString().ShouldBe("Barber");
        item.GetProperty("isActive").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Getting_should_ask_for_the_professional_of_the_route_and_answer_everything_it_has()
    {
        var dispatcher = Answering(UserRole.Professional);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Get, One);
        var body = await TestApi.ReadAsync(response, _ct);

        dispatcher.Requests.Single().ShouldBeOfType<GetProfessionalQuery>().ProfessionalId.ShouldBe(ProfessionalId);
        body.GetProperty("userId").GetGuid().ShouldBe(UserId);

        var hours = body.GetProperty("workingHours").EnumerateArray().Single();
        hours.GetProperty("branchId").GetGuid().ShouldBe(BranchId);
        hours.GetProperty("schedule").GetProperty("day").GetString().ShouldBe("Monday");
        hours.GetProperty("schedule").GetProperty("start").GetString().ShouldBe("09:00:00");

        var timeOff = body.GetProperty("timeOffs").EnumerateArray().Single();
        timeOff.GetProperty("id").GetGuid().ShouldBe(TimeOffId);
        timeOff.GetProperty("startUtc").GetString().ShouldBe("2026-12-01T09:00:00Z");
        timeOff.GetProperty("reason").GetString().ShouldBe("Dentist");

        var offering = body.GetProperty("offerings").EnumerateArray().Single();
        offering.GetProperty("serviceOfferingId").GetGuid().ShouldBe(ServiceId);
        offering.GetProperty("customDurationMinutes").GetInt32().ShouldBe(45);
    }

    [Fact]
    public async Task A_professional_that_does_not_exist_should_be_a_404_with_its_code()
    {
        var dispatcher = StaffApi.As(UserRole.CompanyAdmin)
            .Reply<GetProfessionalQuery, ProfessionalDto>(_ => Error.NotFound("Professional.NotFound"));
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Get, One);
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.GetProperty("code").GetString().ShouldBe("Professional.NotFound");
    }

    // Register and update -----------------------------------------------------------------------------------------

    [Fact]
    public async Task Registering_should_send_the_body_as_the_command_and_answer_201_with_the_location()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "create");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location!.OriginalString.ShouldBe($"{Base}/{NewProfessionalId}");
        body.GetProperty("id").GetGuid().ShouldBe(NewProfessionalId);
        dispatcher.Requests.Single().ShouldBe(new RegisterProfessionalCommand("Ana Souza", "Barber"));
    }

    [Fact]
    public async Task A_professional_with_no_specialty_in_the_body_should_have_none()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Post, Base, """{ "name": "Bruno" }""");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        dispatcher.Requests.Single().ShouldBe(new RegisterProfessionalCommand("Bruno"));
    }

    [Fact]
    public async Task Updating_should_use_the_professional_of_the_route_and_ignore_one_in_the_body()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "update");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(new UpdateProfessionalDetailsCommand(ProfessionalId, "Ana Lima", "Colorist"));
    }

    [Theory]
    [InlineData("activate")]
    [InlineData("deactivate")]
    public async Task Activating_and_deactivating_should_send_the_professional_of_the_route(string name)
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, name);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(
            name == "activate" ? new ActivateProfessionalCommand(ProfessionalId) : new DeactivateProfessionalCommand(ProfessionalId));
    }

    // Working hours -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task Setting_the_hours_should_take_the_ids_and_the_day_from_the_route_and_the_rest_from_the_body()
    {
        var dispatcher = Answering(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "set-hours");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var command = dispatcher.Requests.Single().ShouldBeOfType<SetProfessionalWorkingHoursCommand>();
        command.ProfessionalId.ShouldBe(ProfessionalId);
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

        var response = await CallAsync(
            app, HttpMethod.Put, $"{One}/branches/{BranchId}/working-hours/Tuesday", """{ "start": "08:00:00", "end": "12:00:00" }""");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var schedule = dispatcher.Requests.Single().ShouldBeOfType<SetProfessionalWorkingHoursCommand>().Schedule;
        schedule.Day.ShouldBe(DayOfWeek.Tuesday);
        schedule.Breaks.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_day_that_is_not_a_name_of_a_weekday_should_be_a_400_problem_and_send_nothing()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Put, $"{One}/branches/{BranchId}/working-hours/Funday", HoursJson);
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("code").GetString().ShouldBe("Http.BadRequest");
        dispatcher.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_number_outside_the_weekdays_should_be_a_validation_error_on_the_day(bool setting)
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(
            app,
            setting ? HttpMethod.Put : HttpMethod.Delete,
            $"{One}/branches/{BranchId}/working-hours/9",
            setting ? HoursJson : null);
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("code").GetString().ShouldBe("Validation.Failed");
        body.GetProperty("errors").EnumerateArray().Single().GetProperty("field").GetString().ShouldBe("Day");
        dispatcher.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Removing_the_hours_should_send_the_ids_and_the_day_of_the_route()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "remove-hours");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(new RemoveProfessionalWorkingHoursCommand(ProfessionalId, BranchId, DayOfWeek.Monday));
    }

    // Time off ----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Adding_a_time_off_should_send_the_period_and_answer_201_with_its_id_and_the_location_of_the_professional()
    {
        var dispatcher = Answering(UserRole.Receptionist);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "add-time-off");
        var body = await TestApi.ReadAsync(response, _ct);

        // There is no address for one time off: it is read inside the professional.
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location!.OriginalString.ShouldBe(One);
        body.GetProperty("id").GetGuid().ShouldBe(TimeOffId);
        dispatcher.Requests.Single().ShouldBe(new AddProfessionalTimeOffCommand(ProfessionalId, Start, End, "Dentist"));
    }

    [Fact]
    public async Task A_time_off_with_no_reason_should_have_none()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(
            app, HttpMethod.Post, $"{One}/time-offs", """{ "startUtc": "2026-12-01T09:00:00Z", "endUtc": "2026-12-01T12:00:00Z" }""");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var command = dispatcher.Requests.Single().ShouldBeOfType<AddProfessionalTimeOffCommand>();
        command.Reason.ShouldBeNull();
        command.StartUtc.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Removing_a_time_off_should_send_the_ids_of_the_route()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "remove-time-off");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(new RemoveProfessionalTimeOffCommand(ProfessionalId, TimeOffId));
    }

    // Offerings ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Enabling_a_service_should_take_the_ids_from_the_route_and_the_duration_from_the_body()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "enable-offering");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(new EnableProfessionalOfferingCommand(ProfessionalId, ServiceId, 45));
    }

    [Fact]
    public async Task A_service_enabled_with_an_empty_object_should_use_the_standard_duration()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Put, $"{One}/offerings/{ServiceId}", "{}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(new EnableProfessionalOfferingCommand(ProfessionalId, ServiceId));
    }

    [Fact]
    public async Task A_service_that_does_not_exist_should_be_a_404_with_its_code()
    {
        var dispatcher = StaffApi.As(UserRole.CompanyAdmin)
            .Reply<EnableProfessionalOfferingCommand, Unit>(_ => Error.NotFound("ServiceOffering.NotFound"));
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "enable-offering");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.GetProperty("code").GetString().ShouldBe("ServiceOffering.NotFound");
    }

    [Fact]
    public async Task Disabling_a_service_should_send_the_ids_of_the_route()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "disable-offering");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(new DisableProfessionalOfferingCommand(ProfessionalId, ServiceId));
    }

    // The user ----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Linking_should_take_the_professional_from_the_route_and_the_user_from_the_body()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "link-user");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(new LinkProfessionalUserCommand(ProfessionalId, UserId));
    }

    [Fact]
    public async Task A_user_that_does_not_exist_should_be_a_404_with_its_code()
    {
        var dispatcher = StaffApi.As(UserRole.CompanyAdmin)
            .Reply<LinkProfessionalUserCommand, Unit>(_ => Error.NotFound("User.NotFound"));
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "link-user");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.GetProperty("code").GetString().ShouldBe("User.NotFound");
    }

    [Fact]
    public async Task A_user_linked_to_another_professional_should_be_a_409_with_its_code()
    {
        var dispatcher = StaffApi.As(UserRole.CompanyAdmin)
            .Reply<LinkProfessionalUserCommand, Unit>(_ => Error.Conflict("Professional.UserAlreadyLinked"));
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "link-user");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        body.GetProperty("code").GetString().ShouldBe("Professional.UserAlreadyLinked");
    }

    [Fact]
    public async Task Unlinking_should_send_the_professional_of_the_route()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "unlink-user");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(new UnlinkProfessionalUserCommand(ProfessionalId));
    }
}
