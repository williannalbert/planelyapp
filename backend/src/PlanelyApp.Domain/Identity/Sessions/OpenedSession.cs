namespace PlanelyApp.Domain.Identity.Sessions;

/// <summary>A new session and the refresh token to hand to the customer. The token is never saved.</summary>
public sealed record OpenedSession(CustomerSession Session, string RefreshToken)
{
    // The default ToString would print the secret in any log that prints the object.
    public override string ToString() => $"{nameof(OpenedSession)} {{ Id = {Session.Id}, RefreshToken = *** }}";
}