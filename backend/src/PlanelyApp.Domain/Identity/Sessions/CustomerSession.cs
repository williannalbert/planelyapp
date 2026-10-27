using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.Identity.Sessions;

/// <summary>
/// A customer's login in a company. It lasts for a fixed time and is kept alive with a refresh token, which
/// changes every time it is used and is saved only as a hash.
/// </summary>
public sealed class CustomerSession : AggregateRoot, ITenantEntity
{
    private const int TokenBytes = 32;
    private static readonly TimeSpan MaxLifetime = TimeSpan.FromDays(365);

    private CustomerSession(
        Guid companyId,
        Guid customerId,
        string refreshTokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
    {
        CompanyId = companyId;
        CustomerId = customerId;
        RefreshTokenHash = refreshTokenHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid CompanyId { get; }

    public Guid CustomerId { get; }

    /// <summary>SHA-256 of the current refresh token. Never the token itself.</summary>
    public string RefreshTokenHash { get; private set; }

    public DateTime CreatedAtUtc { get; }

    /// <summary>Fixed when the session opens: renewing the token does not extend it.</summary>
    public DateTime ExpiresAtUtc { get; }

    public DateTime? RevokedAtUtc { get; private set; }

    public static OpenedSession Open(Guid companyId, Guid customerId, TimeSpan lifetime, DateTime nowUtc)
    {
        if (companyId == Guid.Empty)
        {
            throw new DomainException("CustomerSession.CompanyRequired");
        }

        if (customerId == Guid.Empty)
        {
            throw new DomainException("CustomerSession.CustomerRequired");
        }

        if (lifetime <= TimeSpan.Zero || lifetime > MaxLifetime)
        {
            throw new DomainException("CustomerSession.LifetimeInvalid");
        }

        Guard.Utc(nowUtc, "CustomerSession.TimeMustBeUtc");

        var refreshToken = NewToken();
        var session = new CustomerSession(companyId, customerId, HashToken(refreshToken), nowUtc, nowUtc + lifetime);

        return new OpenedSession(session, refreshToken);
    }

    /// <summary>
    /// The hash to look a session up by the refresh token the customer presented. The token is long and
    /// random, so a plain hash is enough (unlike the 6 digit access code).
    /// </summary>
    public static string HashToken(string? refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new DomainException("CustomerSession.RefreshTokenRequired");
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
    }

    public bool IsActive(DateTime nowUtc) => RevokedAtUtc is null && nowUtc < ExpiresAtUtc;

    /// <summary>Replaces the refresh token with a new one and returns it. The old token stops working.</summary>
    public string Renew(DateTime nowUtc)
    {
        Guard.Utc(nowUtc, "CustomerSession.TimeMustBeUtc");

        if (RevokedAtUtc is not null)
        {
            throw new DomainException("CustomerSession.Revoked");
        }

        if (nowUtc >= ExpiresAtUtc)
        {
            throw new DomainException("CustomerSession.Expired");
        }

        var refreshToken = NewToken();
        RefreshTokenHash = HashToken(refreshToken);

        return refreshToken;
    }

    /// <summary>Ends the session (logout). Does nothing if it was already revoked.</summary>
    public void Revoke(DateTime nowUtc)
    {
        Guard.Utc(nowUtc, "CustomerSession.TimeMustBeUtc");

        RevokedAtUtc ??= nowUtc;
    }

    private static string NewToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
}