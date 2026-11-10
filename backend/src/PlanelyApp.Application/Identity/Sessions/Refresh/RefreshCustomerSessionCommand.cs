using PlanelyApp.Application.Abstractions.Cqrs;

namespace PlanelyApp.Application.Identity.Sessions.Refresh;

/// <summary>
/// Trades a refresh token for a new access token and a new refresh token. The old refresh token stops working, and the
/// session keeps its original end: renewing never extends it.
/// </summary>
public sealed record RefreshCustomerSessionCommand(string? RefreshToken) : ICommand<CustomerTokens>
{
    public override string ToString() => $"{nameof(RefreshCustomerSessionCommand)} {{ RefreshToken = *** }}";
}

public sealed record CustomerTokens(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime SessionExpiresAtUtc)
{
    public override string ToString() => $"{nameof(CustomerTokens)} {{ tokens = *** }}";
}