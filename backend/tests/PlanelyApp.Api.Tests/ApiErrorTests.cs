using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using PlanelyApp.Api.Errors;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.SeedWork;
using Shouldly;
using System.Text.Json;

namespace PlanelyApp.Api.Tests;

public class ApiErrorTests
{
    private static readonly Guid CreatedId = Guid.CreateVersion7();

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_domain_exception_should_be_a_422_problem_with_its_code_and_arguments()
    {
        await using var app = await TestApi.StartAsync(api =>
            api.MapGet("/boom", IResult () => throw new DomainException("Customer.ContactRequired", 5, "x")));

        var response = await app.GetTestClient().GetAsync("/boom", _ct);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var body = await TestApi.ReadAsync(response, _ct);
        body.GetProperty("status").GetInt32().ShouldBe(422);
        body.GetProperty("code").GetString().ShouldBe("Customer.ContactRequired");
        body.GetProperty("arguments").EnumerateArray().Select(item => item.GetString()).ShouldBe(["5", "x"]);
        body.GetProperty("correlationId").GetString().ShouldNotBeNullOrWhiteSpace();
        body.TryGetProperty("traceId", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Any_other_exception_should_be_a_500_that_does_not_leak_its_message()
    {
        await using var app = await TestApi.StartAsync(api =>
            api.MapGet("/crash", IResult () => throw new InvalidOperationException("secret detail")));

        var response = await app.GetTestClient().GetAsync("/crash", _ct);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.InternalServerError);
        var text = await response.Content.ReadAsStringAsync(_ct);
        text.ShouldNotContain("secret detail");
        using var body = JsonDocument.Parse(text);
        body.RootElement.GetProperty("code").GetString().ShouldBe("Server.UnexpectedError");
    }

    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    public async Task An_error_of_a_use_case_should_get_the_status_of_its_type(ErrorType type, int status)
    {
        await using var app = await TestApi.StartAsync(api =>
            api.MapGet("/result/{type}", (ErrorType type) => Result<int>.Failure(new Error("Thing.Problem", type)).ToHttp()));

        var response = await app.GetTestClient().GetAsync($"/result/{type}", _ct);

        ((int)response.StatusCode).ShouldBe(status);
        var body = await TestApi.ReadAsync(response, _ct);
        body.GetProperty("code").GetString().ShouldBe("Thing.Problem");
    }

    [Fact]
    public async Task A_validation_error_should_list_the_fields()
    {
        await using var app = await TestApi.StartAsync(api =>
            api.MapPost("/invalid", () => Result<int>.Failure(new ValidationError(
                [new FieldError("Email", "Validation.Invalid"), new FieldError("Name", "Validation.Required")])).ToHttp()));

        var response = await app.GetTestClient().PostAsync("/invalid", null, _ct);

        ((int)response.StatusCode).ShouldBe(400);
        var body = await TestApi.ReadAsync(response, _ct);
        body.GetProperty("code").GetString().ShouldBe("Validation.Failed");
        var errors = body.GetProperty("errors").EnumerateArray().ToList();
        errors.Select(error => error.GetProperty("field").GetString()).ShouldBe(["Email", "Name"]);
        errors.Select(error => error.GetProperty("code").GetString()).ShouldBe(["Validation.Invalid", "Validation.Required"]);
    }

    [Fact]
    public async Task A_success_should_be_a_200_with_the_value()
    {
        await using var app = await TestApi.StartAsync(api =>
            api.MapGet("/ok", () => Result<int>.Success(42).ToHttp()));

        var response = await app.GetTestClient().GetAsync("/ok", _ct);

        ((int)response.StatusCode).ShouldBe(200);
        (await response.Content.ReadAsStringAsync(_ct)).ShouldBe("42");
    }

    [Fact]
    public async Task A_command_with_nothing_to_return_should_be_a_204()
    {
        await using var app = await TestApi.StartAsync(api =>
            api.MapDelete("/nothing", () => Result.Success().ToNoContent()));

        var response = await app.GetTestClient().DeleteAsync("/nothing", _ct);

        ((int)response.StatusCode).ShouldBe(204);
    }

    [Fact]
    public async Task A_creation_should_be_a_201_with_the_location_and_the_id()
    {
        await using var app = await TestApi.StartAsync(api =>
            api.MapPost("/things", () => Result<Guid>.Success(CreatedId).ToCreated(id => $"/things/{id}")));

        var response = await app.GetTestClient().PostAsync("/things", null, _ct);

        ((int)response.StatusCode).ShouldBe(201);
        response.Headers.Location!.ToString().ShouldBe($"/things/{CreatedId}");
        var body = await TestApi.ReadAsync(response, _ct);
        body.GetProperty("id").GetGuid().ShouldBe(CreatedId);
    }

    [Fact]
    public async Task An_unknown_route_should_be_a_404_problem_with_a_code()
    {
        await using var app = await TestApi.StartAsync(_ => { });

        var response = await app.GetTestClient().GetAsync("/nothing-here", _ct);

        ((int)response.StatusCode).ShouldBe(404);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var body = await TestApi.ReadAsync(response, _ct);
        body.GetProperty("code").GetString().ShouldBe("Http.NotFound");
        body.GetProperty("correlationId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task The_liveness_check_should_answer_200()
    {
        await using var app = await TestApi.StartAsync(_ => { });

        var response = await app.GetTestClient().GetAsync("/health/live", _ct);

        ((int)response.StatusCode).ShouldBe(200);
    }
}