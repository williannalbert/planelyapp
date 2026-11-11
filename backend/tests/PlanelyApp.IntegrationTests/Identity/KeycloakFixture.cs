using System.Net;
using System.Text.Json.Nodes;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Infrastructure.Identity;

namespace PlanelyApp.IntegrationTests.Identity;

/// <summary>
/// A real Keycloak with the realm of the project imported from the same file the compose uses, and a Mailpit to
/// receive its e-mails. One pair of containers for all the tests of the class.
/// </summary>
public sealed class KeycloakFixture : IAsyncLifetime
{
    public const string Realm = "planelyapp";
    public const string ClientId = "planelyapp-api";
    public const string ClientSecret = "planelyapp-api-secret";

    private readonly INetwork _network = new NetworkBuilder().Build();
    private readonly IContainer _mailpit;
    private readonly IContainer _keycloak;
    private readonly HttpClient _http = new();
    private ServiceProvider? _services;

    public KeycloakFixture()
    {
        var realm = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Identity", "planelyapp-realm.json"));

        _mailpit = new ContainerBuilder("axllent/mailpit:latest")
            .WithNetwork(_network)
            .WithNetworkAliases("mailpit")
            .WithPortBinding(8025, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request
                .ForPort(8025)
                .ForPath("/api/v1/info")))
            .Build();

        _keycloak = new ContainerBuilder("quay.io/keycloak/keycloak:26.4")
            .WithNetwork(_network)
            .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", "admin")
            .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", "admin")
            .WithResourceMapping(realm, "/opt/keycloak/data/import/planelyapp-realm.json")
            .WithCommand("start-dev", "--import-realm")
            .WithPortBinding(8080, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request
                .ForPort(8080)
                .ForPath("/realms/planelyapp")))
            .Build();
    }

    public IIdentityProvider Provider => _services!.GetRequiredService<IIdentityProvider>();

    private string KeycloakUrl => $"http://{_keycloak.Hostname}:{_keycloak.GetMappedPublicPort(8080)}";

    private string MailpitUrl => $"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(8025)}";

    public async ValueTask InitializeAsync()
    {
        await _network.CreateAsync();
        await Task.WhenAll(_mailpit.StartAsync(), _keycloak.StartAsync());

        _services = new ServiceCollection()
            .AddKeycloakIdentityProvider(new KeycloakOptions
            {
                BaseUrl = KeycloakUrl,
                Realm = Realm,
                ClientId = ClientId,
                ClientSecret = ClientSecret,
                RequestTimeout = TimeSpan.FromSeconds(60),
            })
            .BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        _http.Dispose();
        await _keycloak.DisposeAsync();
        await _mailpit.DisposeAsync();
        await _network.DisposeAsync();
    }

    /// <summary>The account as Keycloak has it, read on its own (not through the adapter). Null if there is none.</summary>
    public async Task<JsonObject?> GetAccountAsync(string identityId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"{KeycloakUrl}/admin/realms/{Realm}/users/{identityId}");
        request.Headers.Authorization = new("Bearer", await GetTokenAsync(cancellationToken));

        using var response = await _http.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken)) as JsonObject;
    }

    /// <summary>The subject of the first e-mail Mailpit received for the address, or null if none comes in 15 seconds.</summary>
    public async Task<string?> WaitForEmailSubjectAsync(string address, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var json = await _http.GetStringAsync($"{MailpitUrl}/api/v1/messages", cancellationToken);
            var messages = JsonNode.Parse(json)?["messages"]?.AsArray() ?? new JsonArray();

            foreach (var message in messages)
            {
                var recipients = message?["To"]?.AsArray();

                if (recipients is not null
                    && recipients.Any(recipient => recipient?["Address"]?.GetValue<string>() == address))
                {
                    return message!["Subject"]?.GetValue<string>();
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }

        return null;
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsync(
            $"{KeycloakUrl}/realms/{Realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
            }),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken))!;

        return json["access_token"]!.GetValue<string>();
    }
}