using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PlanelyApp.Application.Abstractions.Identity;

namespace PlanelyApp.Infrastructure.Identity;

/// <summary>Logs in as the service client and keeps the token until it is about to expire.</summary>
internal sealed class KeycloakTokenProvider(HttpClient http, KeycloakOptions options, TimeProvider clock)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile CachedToken? _cached;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (Fresh() is { } token)
        {
            return token;
        }

        await _gate.WaitAsync(cancellationToken);

        try
        {
            // Another request may have renewed it while this one waited.
            return Fresh() ?? await RequestAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>For when Keycloak refused the token before it expired (a restart, a revoked session).</summary>
    public void Invalidate() => _cached = null;

    private string? Fresh() =>
        _cached is { } cached && clock.GetUtcNow() < cached.ExpiresAt ? cached.Value : null;

    private async Task<string> RequestAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"realms/{options.Realm}/protocol/openid-connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = options.ClientId,
                ["client_secret"] = options.ClientSecret,
            }),
        };

        TokenResponse? token;

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new IdentityProviderException(
                    $"Keycloak refused the login of the service client: {(int)response.StatusCode}.");
            }

            token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new IdentityProviderException("Keycloak could not be reached.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new IdentityProviderException("Keycloak did not answer in time.", exception);
        }

        if (token is null || string.IsNullOrEmpty(token.AccessToken))
        {
            throw new IdentityProviderException("Keycloak answered the login without a token.");
        }

        // Renewed a little before the end, so a request never leaves with a token that dies on the way.
        _cached = new CachedToken(
            token.AccessToken, clock.GetUtcNow().AddSeconds(Math.Max(token.ExpiresIn - 30, 5)));

        return token.AccessToken;
    }

    private sealed record CachedToken(string Value, DateTimeOffset ExpiresAt);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}