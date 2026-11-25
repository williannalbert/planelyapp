namespace PlanelyApp.Infrastructure.Development;

/// <summary>
/// The platform administrator of the development environment: the user of the Keycloak realm that the Api must also know.
/// Values of development only; the section exists only in <c>appsettings.Development.json</c>.
/// </summary>
public sealed class DevelopmentSeedOptions
{
    /// <summary>The <c>sub</c> of the token of the realm user.</summary>
    public string IdentityId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(IdentityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(Email);
    }
}