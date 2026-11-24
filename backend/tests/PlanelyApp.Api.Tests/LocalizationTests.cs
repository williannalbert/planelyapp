using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using PlanelyApp.Api.Errors;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.SeedWork;
using Shouldly;
using System.Text.Json;

namespace PlanelyApp.Api.Tests;

/// <summary>The language of the answer: Accept-Language picks it, Brazilian Portuguese is the default.</summary>
public class LocalizationTests
{
    private const string Portuguese = "O idioma \"fr\" não é suportado.";
    private const string English = "The language \"fr\" is not supported.";
    private const string Spanish = "El idioma \"fr\" no es compatible.";

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static Task<WebApplication> StartAsync() => TestApi.StartAsync(api =>
    {
        api.MapGet("/rule", IResult () => throw new DomainException("Language.NotSupported", "fr"));
        api.MapGet("/transition", IResult () => throw new DomainException("Appointment.InvalidStatusTransition", "Pending", "Completed"));
        api.MapGet("/unknown", IResult () => throw new DomainException("Test.NoTextForThis"));
        api.MapGet("/crash", IResult () => throw new InvalidOperationException("secret detail"));
        api.MapGet("/missing-customer", () => Result<int>.Failure(Error.NotFound("Customer.NotFound")).ToHttp());
        api.MapGet("/invalid", () => Result<int>.Failure(new ValidationError(
            [new FieldError("Email", "Validation.Invalid"), new FieldError("Name", "Validation.Required")])).ToHttp());
    });

    private async Task<(HttpResponseMessage Response, JsonElement Body)> GetAsync(
        WebApplication app,
        string path,
        string? acceptLanguage)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (acceptLanguage is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);
        }

        var response = await app.GetTestClient().SendAsync(request, _ct);

        return (response, await TestApi.ReadAsync(response, _ct));
    }

    private static string? HeaderOf(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) || response.Content.Headers.TryGetValues(name, out values)
            ? string.Join(",", values)
            : null;

    [Fact]
    public async Task Without_accept_language_the_detail_should_be_in_brazilian_portuguese()
    {
        await using var app = await StartAsync();

        var (response, body) = await GetAsync(app, "/rule", null);

        body.GetProperty("detail").GetString().ShouldBe(Portuguese);
        body.GetProperty("code").GetString().ShouldBe("Language.NotSupported");
        HeaderOf(response, "Content-Language").ShouldBe("pt-BR");
    }

    [Theory]
    [InlineData("en", English, "en")]
    [InlineData("en-US,en;q=0.9", English, "en")]
    [InlineData("es", Spanish, "es")]
    [InlineData("es-AR", Spanish, "es")]
    [InlineData("pt-BR", Portuguese, "pt-BR")]
    [InlineData("es;q=0.9,en;q=0.5", Spanish, "es")]
    [InlineData("en;q=0.5,es;q=0.9", Spanish, "es")]
    [InlineData("fr-FR,fr;q=0.9,en;q=0.8", English, "en")]
    public async Task The_language_asked_for_should_pick_the_text(string acceptLanguage, string expected, string contentLanguage)
    {
        await using var app = await StartAsync();

        var (response, body) = await GetAsync(app, "/rule", acceptLanguage);

        body.GetProperty("detail").GetString().ShouldBe(expected);
        HeaderOf(response, "Content-Language").ShouldBe(contentLanguage);
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("pt-PT")]
    [InlineData("*")]
    [InlineData("not a language;;;")]
    public async Task A_language_that_is_not_supported_should_get_the_default(string acceptLanguage)
    {
        await using var app = await StartAsync();

        var (_, body) = await GetAsync(app, "/rule", acceptLanguage);

        body.GetProperty("detail").GetString().ShouldBe(Portuguese);
    }

    [Fact]
    public async Task The_query_string_should_not_pick_the_language()
    {
        await using var app = await StartAsync();

        var (_, body) = await GetAsync(app, "/rule?culture=en&ui-culture=en", null);

        body.GetProperty("detail").GetString().ShouldBe(Portuguese);
    }

    [Theory]
    [InlineData("pt-BR", "O agendamento não pode passar do estado Pending para Completed.")]
    [InlineData("en", "The appointment cannot go from Pending to Completed.")]
    [InlineData("es", "La cita no puede pasar de Pending a Completed.")]
    public async Task The_arguments_should_fill_the_placeholders(string acceptLanguage, string expected)
    {
        await using var app = await StartAsync();

        var (_, body) = await GetAsync(app, "/transition", acceptLanguage);

        body.GetProperty("detail").GetString().ShouldBe(expected);
        body.GetProperty("arguments").EnumerateArray().Select(item => item.GetString()).ShouldBe(["Pending", "Completed"]);
    }

    [Fact]
    public async Task A_code_with_no_text_should_have_no_detail_and_keep_its_code()
    {
        await using var app = await StartAsync();

        var (response, body) = await GetAsync(app, "/unknown", "en");

        ((int)response.StatusCode).ShouldBe(422);
        body.GetProperty("code").GetString().ShouldBe("Test.NoTextForThis");
        body.TryGetProperty("detail", out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("pt-BR", "O cliente não foi encontrado.")]
    [InlineData("en", "The customer was not found.")]
    [InlineData("es", "El cliente no se encontró.")]
    public async Task An_error_of_a_use_case_should_have_its_detail_too(string acceptLanguage, string expected)
    {
        await using var app = await StartAsync();

        var (response, body) = await GetAsync(app, "/missing-customer", acceptLanguage);

        ((int)response.StatusCode).ShouldBe(404);
        body.GetProperty("detail").GetString().ShouldBe(expected);
        HeaderOf(response, "Content-Language").ShouldBe(acceptLanguage);
    }

    [Fact]
    public async Task Each_field_error_should_have_its_message_in_the_language_of_the_request()
    {
        await using var app = await StartAsync();

        var (response, body) = await GetAsync(app, "/invalid", "es");

        ((int)response.StatusCode).ShouldBe(400);
        body.GetProperty("detail").GetString().ShouldBe("La solicitud tiene campos no válidos.");
        var errors = body.GetProperty("errors").EnumerateArray().ToList();
        errors.Select(error => error.GetProperty("field").GetString()).ShouldBe(["Email", "Name"]);
        errors.Select(error => error.GetProperty("code").GetString()).ShouldBe(["Validation.Invalid", "Validation.Required"]);
        errors.Select(error => error.GetProperty("message").GetString()).ShouldBe(["Valor no válido.", "Campo obligatorio."]);
    }

    [Fact]
    public async Task A_status_with_no_body_should_have_its_detail_too()
    {
        await using var app = await StartAsync();

        var (response, body) = await GetAsync(app, "/there-is-no-such-route", "en");

        ((int)response.StatusCode).ShouldBe(404);
        body.GetProperty("code").GetString().ShouldBe("Http.NotFound");
        body.GetProperty("detail").GetString().ShouldBe("The resource was not found.");
        HeaderOf(response, "Content-Language").ShouldBe("en");
    }

    [Fact]
    public async Task An_unexpected_failure_should_have_a_translated_detail_that_does_not_leak_the_exception()
    {
        await using var app = await StartAsync();

        var (response, body) = await GetAsync(app, "/crash", "en");

        ((int)response.StatusCode).ShouldBe(500);
        body.GetProperty("code").GetString().ShouldBe("Server.UnexpectedError");
        body.GetProperty("detail").GetString().ShouldBe("An unexpected error happened. Try again later.");
        HeaderOf(response, "Content-Language").ShouldBe("en");
        body.ToString().ShouldNotContain("secret detail");
    }
}
