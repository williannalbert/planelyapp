using System.Net;
using Microsoft.AspNetCore.Builder;
using PlanelyApp.Api.Endpoints.Organization.Users;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Users;
using PlanelyApp.Application.Organization.Users.Activate;
using PlanelyApp.Application.Organization.Users.ChangeRole;
using PlanelyApp.Application.Organization.Users.Create;
using PlanelyApp.Application.Organization.Users.Deactivate;
using PlanelyApp.Application.Organization.Users.Get;
using PlanelyApp.Application.Organization.Users.List;
using PlanelyApp.Application.Organization.Users.SetBranchAccess;
using PlanelyApp.Application.Organization.Users.UpdateDetails;
using PlanelyApp.Domain.Organization.Users;
using Shouldly;

namespace PlanelyApp.Api.Tests;

/// <summary>
/// The user endpoints (the accounts of the team) read the route, the query string and the body, send one command or query and turn the
/// result into HTTP. They are for the company administrator alone. The role is written as a name, and a role the body leaves out is a
/// 400 on the field: the enum has <c>PlatformAdmin</c> as its first member, so a missing role would arrive as the one role nobody may
/// be given, after the account was already created at the identity provider. The rules of the user are in the use cases.
/// </summary>
public class UserEndpointsTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly Guid NewUserId = Guid.CreateVersion7();
    private static readonly Guid BranchA = Guid.CreateVersion7();
    private static readonly Guid BranchB = Guid.CreateVersion7();
    private static readonly Guid OtherCompany = Guid.CreateVersion7();

    private static readonly string Base = $"/companies/{StaffApi.Company}/users";
    private static readonly string One = $"{Base}/{UserId}";

    private static readonly string CreateJson = $$"""
        { "name": "Carla Dias", "email": "carla@example.com", "role": "Receptionist", "accessAllBranches": false,
          "branchIds": ["{{BranchA}}", "{{BranchB}}"], "preferredLanguage": "pt-BR" }
        """;

    private const string UpdateJson = """{ "userId": "00000000-0000-0000-0000-000000000001", "name": "Carla Lima", "email": "carla.lima@example.com", "preferredLanguage": "en" }""";
    private const string RoleJson = """{ "role": "Professional" }""";
    private const string AccessJson = """{ "accessAllBranches": true }""";

    private static readonly IReadOnlyList<UserSummaryDto> Summaries =
        [new(UserId, "Carla Dias", "carla@example.com", UserRole.Receptionist, true, false)];

    private static readonly UserDto Details = new(
        UserId, "Carla Dias", "carla@example.com", UserRole.Receptionist, false, [BranchA, BranchB], "pt-BR", true);

    private static readonly UserRole[] AdminOnly = [UserRole.CompanyAdmin];

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    /// <summary>Which roles each endpoint accepts, and what it answers when it works.</summary>
    private static readonly Dictionary<string, (UserRole[] Roles, HttpStatusCode Success)> Rules = new()
    {
        ["list"] = (AdminOnly, HttpStatusCode.OK),
        ["create"] = (AdminOnly, HttpStatusCode.Created),
        ["get"] = (AdminOnly, HttpStatusCode.OK),
        ["update"] = (AdminOnly, HttpStatusCode.NoContent),
        ["change-role"] = (AdminOnly, HttpStatusCode.NoContent),
        ["set-branch-access"] = (AdminOnly, HttpStatusCode.NoContent),
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
        .Reply<ListUsersQuery, IReadOnlyList<UserSummaryDto>>(_ => Result<IReadOnlyList<UserSummaryDto>>.Success(Summaries))
        .Reply<GetUserQuery, UserDto>(_ => Details)
        .Reply<CreateUserCommand, Guid>(_ => NewUserId)
        .Reply<UpdateUserDetailsCommand, Unit>(_ => Unit.Value)
        .Reply<ChangeUserRoleCommand, Unit>(_ => Unit.Value)
        .Reply<SetUserBranchAccessCommand, Unit>(_ => Unit.Value)
        .Reply<ActivateUserCommand, Unit>(_ => Unit.Value)
        .Reply<DeactivateUserCommand, Unit>(_ => Unit.Value);

    private static FakeDispatcher Answering(UserRole role) => WithReplies(StaffApi.As(role));

    private static (HttpMethod Method, string Path, string? Json) Route(string name, Guid? company = null)
    {
        var root = $"/companies/{company ?? StaffApi.Company}/users";
        var one = $"{root}/{UserId}";

        return name switch
        {
            "list" => (HttpMethod.Get, root, null),
            "create" => (HttpMethod.Post, root, CreateJson),
            "get" => (HttpMethod.Get, one, null),
            "update" => (HttpMethod.Put, one, UpdateJson),
            "change-role" => (HttpMethod.Put, $"{one}/role", RoleJson),
            "set-branch-access" => (HttpMethod.Put, $"{one}/branch-access", AccessJson),
            "activate" => (HttpMethod.Post, $"{one}/activate", null),
            "deactivate" => (HttpMethod.Post, $"{one}/deactivate", null),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };
    }

    private static Task<WebApplication> StartAsync(FakeDispatcher dispatcher) =>
        StaffApi.StartAsync(dispatcher, api => api.MapUsers());

    private Task<HttpResponseMessage> SendAsync(WebApplication app, string name, bool signedIn = true, Guid? company = null)
    {
        var (method, path, json) = Route(name, company);

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
    public async Task Each_endpoint_should_accept_only_the_company_administrator_and_answer_with_its_status(string name, UserRole role)
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
    [InlineData("", null, null)]
    [InlineData("?role=Receptionist", UserRole.Receptionist, null)]
    [InlineData("?isActive=false", null, false)]
    [InlineData("?role=Professional&isActive=true", UserRole.Professional, true)]
    public async Task Listing_should_pass_the_filters_of_the_query_string(string query, UserRole? role, bool? isActive)
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Get, Base + query);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        dispatcher.Requests.Single().ShouldBe(new ListUsersQuery(role, isActive));
    }

    [Fact]
    public async Task A_number_in_place_of_the_name_of_a_role_should_be_a_validation_error_on_the_field()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Get, Base + "?role=9");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("code").GetString().ShouldBe("Validation.Failed");
        var error = body.GetProperty("errors").EnumerateArray().Single();
        error.GetProperty("field").GetString().ShouldBe("Role");
        error.GetProperty("code").GetString().ShouldBe("Validation.Invalid");
        dispatcher.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_role_that_has_no_such_name_should_be_a_400_and_send_nothing()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Get, Base + "?role=Nobody");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("code").GetString().ShouldBe("Http.BadRequest");
        dispatcher.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Listing_should_answer_an_array_in_camel_case_with_the_role_as_a_name()
    {
        await using var app = await StartAsync(Answering(UserRole.CompanyAdmin));

        var body = await TestApi.ReadAsync(await CallAsync(app, HttpMethod.Get, Base), _ct);

        var item = body.EnumerateArray().Single();
        item.GetProperty("id").GetGuid().ShouldBe(UserId);
        item.GetProperty("name").GetString().ShouldBe("Carla Dias");
        item.GetProperty("email").GetString().ShouldBe("carla@example.com");
        item.GetProperty("role").GetString().ShouldBe("Receptionist");
        item.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        item.GetProperty("accessAllBranches").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Getting_should_ask_for_the_user_of_the_route_and_answer_the_branches_it_sees()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Get, One);
        var body = await TestApi.ReadAsync(response, _ct);

        dispatcher.Requests.Single().ShouldBe(new GetUserQuery(UserId));
        body.GetProperty("role").GetString().ShouldBe("Receptionist");
        body.GetProperty("accessAllBranches").GetBoolean().ShouldBeFalse();
        body.GetProperty("branchIds").EnumerateArray().Select(id => id.GetGuid()).ShouldBe([BranchA, BranchB]);
        body.GetProperty("preferredLanguage").GetString().ShouldBe("pt-BR");
    }

    [Fact]
    public async Task A_user_that_does_not_exist_should_be_a_404_with_its_code()
    {
        var dispatcher = StaffApi.As(UserRole.CompanyAdmin)
            .Reply<GetUserQuery, UserDto>(_ => Error.NotFound("User.NotFound"));
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Get, One);
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.GetProperty("code").GetString().ShouldBe("User.NotFound");
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
        response.Headers.Location!.OriginalString.ShouldBe($"{Base}/{NewUserId}");
        body.GetProperty("id").GetGuid().ShouldBe(NewUserId);

        var command = dispatcher.Requests.Single().ShouldBeOfType<CreateUserCommand>();
        command.Name.ShouldBe("Carla Dias");
        command.Email.ShouldBe("carla@example.com");
        command.Role.ShouldBe(UserRole.Receptionist);
        command.AccessAllBranches.ShouldBeFalse();
        command.BranchIds.ShouldBe([BranchA, BranchB]);
        command.PreferredLanguage.ShouldBe("pt-BR");
    }

    [Fact]
    public async Task A_user_with_only_the_name_the_e_mail_and_the_role_should_see_no_branch_and_have_no_language()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(
            app, HttpMethod.Post, Base, """{ "name": "Dani", "email": "dani@example.com", "role": "Professional" }""");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var command = dispatcher.Requests.Single().ShouldBeOfType<CreateUserCommand>();
        command.Role.ShouldBe(UserRole.Professional);
        command.AccessAllBranches.ShouldBeFalse();
        command.BranchIds.ShouldBeNull();
        command.PreferredLanguage.ShouldBeNull();
    }

    [Theory]
    [InlineData("""{ "name": "Dani", "email": "dani@example.com" }""")]
    [InlineData("""{ "name": "Dani", "email": "dani@example.com", "role": null }""")]
    public async Task A_user_with_no_role_in_the_body_should_be_a_400_on_the_field_before_any_account_is_created(string json)
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Post, Base, json);

        await AssertRequiredAsync(response, "Role", dispatcher);
    }

    [Fact]
    public async Task A_role_written_as_a_number_should_be_a_400_and_send_nothing()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(
            app, HttpMethod.Post, Base, """{ "name": "Dani", "email": "dani@example.com", "role": 1 }""");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("code").GetString().ShouldBe("Http.BadRequest");
        dispatcher.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_e_mail_that_is_already_registered_should_be_a_409_with_its_code()
    {
        var dispatcher = StaffApi.As(UserRole.CompanyAdmin)
            .Reply<CreateUserCommand, Guid>(_ => Error.Conflict("User.EmailAlreadyRegistered"));
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "create");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        body.GetProperty("code").GetString().ShouldBe("User.EmailAlreadyRegistered");
    }

    // Update, role and activation ---------------------------------------------------------------------------------

    [Fact]
    public async Task Updating_should_use_the_user_of_the_route_and_ignore_one_in_the_body()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "update");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(new UpdateUserDetailsCommand(UserId, "Carla Lima", "carla.lima@example.com", "en"));
    }

    [Fact]
    public async Task Changing_the_role_should_use_the_user_of_the_route_and_the_role_of_the_body()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "change-role");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(new ChangeUserRoleCommand(UserId, UserRole.Professional));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "role": null }""")]
    public async Task A_change_of_role_with_no_role_should_be_a_400_on_the_field(string json)
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Put, $"{One}/role", json);

        await AssertRequiredAsync(response, "Role", dispatcher);
    }

    [Fact]
    public async Task Taking_the_role_of_the_last_administrator_should_be_a_409_with_its_code()
    {
        var dispatcher = StaffApi.As(UserRole.CompanyAdmin)
            .Reply<ChangeUserRoleCommand, Unit>(_ => Error.Conflict("User.LastAdministrator"));
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "change-role");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        body.GetProperty("code").GetString().ShouldBe("User.LastAdministrator");
    }

    [Theory]
    [InlineData("activate")]
    [InlineData("deactivate")]
    public async Task Activating_and_deactivating_should_send_the_user_of_the_route(string name)
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, name);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        dispatcher.Requests.Single().ShouldBe(
            name == "activate" ? new ActivateUserCommand(UserId) : new DeactivateUserCommand(UserId));
    }

    // Branch access -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task Giving_access_to_every_branch_should_need_no_list()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await SendAsync(app, "set-branch-access");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var command = dispatcher.Requests.Single().ShouldBeOfType<SetUserBranchAccessCommand>();
        command.UserId.ShouldBe(UserId);
        command.AccessAllBranches.ShouldBeTrue();
        command.BranchIds.ShouldBeNull();
    }

    [Fact]
    public async Task Restricting_the_access_should_send_the_list_of_branches_of_the_body()
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(
            app, HttpMethod.Put, $"{One}/branch-access", $$"""{ "accessAllBranches": false, "branchIds": ["{{BranchA}}"] }""");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var command = dispatcher.Requests.Single().ShouldBeOfType<SetUserBranchAccessCommand>();
        command.AccessAllBranches.ShouldBeFalse();
        command.BranchIds.ShouldBe([BranchA]);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "branchIds": [] }""")]
    public async Task A_body_that_does_not_say_whether_the_user_sees_every_branch_should_be_a_400_on_the_field(string json)
    {
        var dispatcher = Answering(UserRole.CompanyAdmin);
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(app, HttpMethod.Put, $"{One}/branch-access", json);

        await AssertRequiredAsync(response, "AccessAllBranches", dispatcher);
    }

    [Fact]
    public async Task A_branch_of_another_company_should_be_a_404_with_its_code()
    {
        var dispatcher = StaffApi.As(UserRole.CompanyAdmin)
            .Reply<SetUserBranchAccessCommand, Unit>(_ => Error.NotFound("Branch.NotFound"));
        await using var app = await StartAsync(dispatcher);

        var response = await CallAsync(
            app, HttpMethod.Put, $"{One}/branch-access", $$"""{ "accessAllBranches": false, "branchIds": ["{{BranchA}}"] }""");
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.GetProperty("code").GetString().ShouldBe("Branch.NotFound");
    }
}
