namespace PlanelyApp.Infrastructure.Identity;

public sealed class KeycloakOptions
{
    /// <summary>Where Keycloak answers, like http://localhost:8080 (no path).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string Realm { get; set; } = string.Empty;

    /// <summary>The service client the Application logs in with (client credentials).</summary>
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Whether a new account gets an e-mail to set its password.</summary>
    public bool SendSetupEmail { get; set; } = true;

    public TimeSpan SetupEmailLifespan { get; set; } = TimeSpan.FromDays(3);

    /// <summary>The longest a call may take, retries included.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>The longest one attempt may take. A slower one is cut and, when it is a read, tried again.</summary>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(5);

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(BaseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(Realm);
        ArgumentException.ThrowIfNullOrWhiteSpace(ClientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ClientSecret);

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException("The Keycloak base URL must be absolute.", nameof(BaseUrl));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(SetupEmailLifespan, TimeSpan.FromMinutes(1));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(RequestTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(AttemptTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(AttemptTimeout, RequestTimeout);
    }
}