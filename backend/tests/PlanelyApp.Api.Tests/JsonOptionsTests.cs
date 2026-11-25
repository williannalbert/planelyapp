using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using PlanelyApp.Domain.Organization.Users;
using Shouldly;

namespace PlanelyApp.Api.Tests;

/// <summary>An enum travels as its name in both directions, and only as its name: the number of a member is not a contract.</summary>
public class JsonOptionsTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static Task<WebApplication> StartAsync() => TestApi.StartAsync(api =>
    {
        api.MapGet("/role", () => new RoleBody(UserRole.CompanyAdmin));
        api.MapGet("/no-role", () => new RoleBody(null));
        api.MapPost("/role", (RoleBody body) => Results.Ok(new { received = body.Role.ToString() }));
    });

    [Fact]
    public async Task An_enum_should_be_written_as_its_name()
    {
        await using var app = await StartAsync();

        var body = await TestApi.ReadAsync(await app.GetTestClient().GetAsync("/role", _ct), _ct);

        body.GetProperty("role").GetString().ShouldBe("CompanyAdmin");
    }

    [Fact]
    public async Task An_enum_with_no_value_should_be_written_as_null()
    {
        await using var app = await StartAsync();

        var body = await TestApi.ReadAsync(await app.GetTestClient().GetAsync("/no-role", _ct), _ct);

        body.GetProperty("role").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task An_enum_should_be_read_from_its_name()
    {
        await using var app = await StartAsync();

        var response = await app.GetTestClient().PostAsync("/role", StaffApi.Json("{ \"role\": \"Receptionist\" }"), _ct);
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.GetProperty("received").GetString().ShouldBe("Receptionist");
    }

    [Theory]
    [InlineData("{ \"role\": 1 }")]
    [InlineData("{ \"role\": \"Nobody\" }")]
    public async Task A_number_or_an_unknown_name_should_be_a_bad_request(string json)
    {
        await using var app = await StartAsync();

        var response = await app.GetTestClient().PostAsync("/role", StaffApi.Json(json), _ct);
        var body = await TestApi.ReadAsync(response, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("code").GetString().ShouldBe("Http.BadRequest");
    }

    private sealed record RoleBody(UserRole? Role);
}
