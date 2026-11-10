using PlanelyApp.Domain.Identity.AccessCodes;

namespace PlanelyApp.Application.Identity.AccessCodes.Verify;

/// <summary>
/// What happened to the code. It is a value and not an error because a wrong attempt must be saved (it counts toward
/// the limit of 5), and the transaction only saves when the result is a success. <see cref="Login"/> exists only when
/// <see cref="Outcome"/> is <see cref="AccessCodeVerification.Verified"/>.
/// </summary>
public sealed record VerifyAccessCodeResponse(AccessCodeVerification Outcome, CustomerLogin? Login);

public sealed record CustomerLogin(
    Guid CustomerId,
    bool IsNewCustomer,
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime SessionExpiresAtUtc)
{
    public override string ToString() =>
        $"{nameof(CustomerLogin)} {{ CustomerId = {CustomerId}, IsNewCustomer = {IsNewCustomer}, tokens = *** }}";
}