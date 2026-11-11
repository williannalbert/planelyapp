using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Identity.Sessions.Logout;

/// <summary>Ends the session of the refresh token. It succeeds when the token is unknown or already revoked: logging out twice is not an error.</summary>
public sealed record LogoutCustomerCommand(string? RefreshToken) : ICommand<Unit>
{
    public override string ToString() => $"{nameof(LogoutCustomerCommand)} {{ RefreshToken = *** }}";
}