namespace PlanelyApp.Application.Identity.Sessions;

public sealed class CustomerSessionOptions
{
    /// <summary>How long a login lasts, whatever the refresh tokens do. The Domain refuses more than 365 days.</summary>
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromDays(30);
}