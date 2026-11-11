using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Infrastructure.Identity;

/// <summary>
/// The only code that knows the Keycloak Admin API. Keycloak keeps name, e-mail, language and whether the account is
/// enabled; the role and the branches of a user stay in the database.
/// </summary>
internal sealed class KeycloakIdentityProvider(HttpClient http, KeycloakOptions options, KeycloakTokenProvider tokens)
    : IIdentityProvider, IDisposable
{
    private readonly string _usersPath = $"admin/realms/{options.Realm}/users";

    public async Task<Result<string>> CreateUserAsync(IdentityProfile profile, CancellationToken cancellationToken)
    {
        // The realm logs in by e-mail, so the e-mail is also the username.
        var user = new JsonObject
        {
            ["username"] = profile.Email.Value,
            ["email"] = profile.Email.Value,
            ["firstName"] = profile.Name,
            ["enabled"] = true,
        };

        if (profile.Language is { } language)
        {
            user["attributes"] = new JsonObject { ["locale"] = new JsonArray(JsonValue.Create(language.Code)) };
        }

        using var response = await SendAsync(HttpMethod.Post, _usersPath, user, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return Error.Conflict("Identity.EmailAlreadyInUse");
        }

        await EnsureSuccessAsync(response, "create the account", cancellationToken);

        // Keycloak answers 201 with the address of the new account; its last part is the id.
        var identityId = response.Headers.Location?.Segments[^1].Trim('/');

        if (string.IsNullOrEmpty(identityId))
        {
            throw new IdentityProviderException("Keycloak created the account but did not say its id.");
        }

        if (options.SendSetupEmail)
        {
            await SendSetupEmailAsync(identityId, cancellationToken);
        }

        return identityId;
    }

    public async Task<Result<Unit>> UpdateUserAsync(
        string identityId,
        IdentityProfile profile,
        CancellationToken cancellationToken)
    {
        // The update replaces what it is not sent, so the account is read, changed and sent back whole.
        var user = await GetUserAsync(identityId, cancellationToken);

        user["username"] = profile.Email.Value;
        user["email"] = profile.Email.Value;
        user["firstName"] = profile.Name;

        var attributes = user["attributes"] as JsonObject;

        if (attributes is null)
        {
            attributes = new JsonObject();
            user["attributes"] = attributes;
        }

        if (profile.Language is { } language)
        {
            attributes["locale"] = new JsonArray(JsonValue.Create(language.Code));
        }
        else
        {
            attributes.Remove("locale");
        }

        using var response = await SendAsync(HttpMethod.Put, $"{_usersPath}/{identityId}", user, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return Error.Conflict("Identity.EmailAlreadyInUse");
        }

        await EnsureSuccessAsync(response, "update the account", cancellationToken);

        return Result.Success();
    }

    public async Task SetEnabledAsync(string identityId, bool enabled, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(identityId, cancellationToken);

        user["enabled"] = enabled;

        using var response = await SendAsync(HttpMethod.Put, $"{_usersPath}/{identityId}", user, cancellationToken);

        await EnsureSuccessAsync(response, enabled ? "enable the account" : "disable the account", cancellationToken);
    }

    public async Task DeleteUserAsync(string identityId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"{_usersPath}/{identityId}", null, cancellationToken);

        // Already gone is what was asked for.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        await EnsureSuccessAsync(response, "delete the account", cancellationToken);
    }

    public void Dispose() => http.Dispose();

    private async Task SendSetupEmailAsync(string identityId, CancellationToken cancellationToken)
    {
        var lifespan = (int)options.SetupEmailLifespan.TotalSeconds;
        var path = $"{_usersPath}/{identityId}/execute-actions-email?lifespan={lifespan}";

        try
        {
            using var response = await SendAsync(
                HttpMethod.Put, path, new JsonArray(JsonValue.Create("UPDATE_PASSWORD")), cancellationToken);

            await EnsureSuccessAsync(response, "send the first access e-mail", cancellationToken);
        }
        catch
        {
            // An account nobody can set a password for is useless, and the caller never learned its id: undo it here.
            await DeleteUserAsync(identityId, CancellationToken.None);

            throw;
        }
    }

    private async Task<JsonObject> GetUserAsync(string identityId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{_usersPath}/{identityId}", null, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new IdentityProviderException($"Keycloak has no account {identityId}.");
        }

        await EnsureSuccessAsync(response, "read the account", cancellationToken);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        return JsonNode.Parse(json) as JsonObject
            ?? throw new IdentityProviderException("Keycloak answered an account that is not a JSON object.");
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        JsonNode? body,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", await tokens.GetTokenAsync(cancellationToken));

            if (body is not null)
            {
                request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            }

            HttpResponseMessage response;

            try
            {
                response = await http.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                throw new IdentityProviderException("Keycloak could not be reached.", exception);
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new IdentityProviderException("Keycloak did not answer in time.", exception);
            }

            // A token Keycloak no longer accepts: ask for another and try once more.
            if (response.StatusCode != HttpStatusCode.Unauthorized || attempt > 0)
            {
                return response;
            }

            response.Dispose();
            tokens.Invalidate();
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string action,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (body.Length > 300)
        {
            body = body[..300];
        }

        throw new IdentityProviderException($"Keycloak could not {action}: {(int)response.StatusCode} {body}");
    }
}