using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Domain.Identity.AccessCodes;

/// <summary>
/// The code a customer receives to log in without a password. It has 6 digits, lasts 5 minutes, works once
/// and is saved only as a hash (RN21). It accepts at most 5 attempts, and a destination gets at most one new
/// code per minute and five per hour (RN22).
/// </summary>
public sealed class AccessCode : AggregateRoot, ITenantEntity
{
    public const int CodeLength = 6;
    public const int MaxAttempts = 5;
    public const int MaxCodesPerHour = 5;

    public static readonly TimeSpan ValidFor = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MinimumResendInterval = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan ResendWindow = TimeSpan.FromHours(1);

    private AccessCode(Guid companyId, AccessChannel channel, string destination, DateTime createdAtUtc)
    {
        CompanyId = companyId;
        Channel = channel;
        Destination = destination;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = createdAtUtc + ValidFor;
        CodeHash = string.Empty;
    }

    public Guid CompanyId { get; }

    public AccessChannel Channel { get; }

    /// <summary>The normalized e-mail or phone (E.164) the code was sent to.</summary>
    public string Destination { get; }

    /// <summary>SHA-256 of the code, using the Id of this access code as salt. Never the code itself.</summary>
    public string CodeHash { get; private set; }

    public DateTime CreatedAtUtc { get; }

    public DateTime ExpiresAtUtc { get; }

    /// <summary>How many wrong codes were entered.</summary>
    public int Attempts { get; private set; }

    public DateTime? UsedAtUtc { get; private set; }

    /// <summary>
    /// Creates a code for the destination. <paramref name="recentCreationTimesUtc"/> are the creation instants
    /// of the codes already issued to the same destination in the last hour, which the caller loads (RN22).
    /// </summary>
    public static IssuedAccessCode Issue(
        Guid companyId,
        AccessChannel channel,
        string? destination,
        IEnumerable<DateTime> recentCreationTimesUtc,
        DateTime nowUtc)
    {
        if (companyId == Guid.Empty)
        {
            throw new DomainException("AccessCode.CompanyRequired");
        }

        if (!Enum.IsDefined(channel))
        {
            throw new DomainException("AccessCode.ChannelInvalid");
        }

        Guard.Utc(nowUtc, "AccessCode.TimeMustBeUtc");

        var validDestination = channel == AccessChannel.Email
            ? Email.Create(destination).Value
            : Phone.Create(destination).Value;

        EnsureCanIssue(recentCreationTimesUtc.ToList(), nowUtc);

        var plainCode = RandomNumberGenerator.GetInt32(0, 1_000_000)
            .ToString("D" + CodeLength, CultureInfo.InvariantCulture);

        var accessCode = new AccessCode(companyId, channel, validDestination, nowUtc);
        accessCode.CodeHash = Convert.ToHexStringLower(ComputeHash(accessCode.Id, plainCode));

        return new IssuedAccessCode(accessCode, plainCode);
    }

    public bool IsExpired(DateTime nowUtc) => nowUtc >= ExpiresAtUtc;

    /// <summary>
    /// Checks the code the customer entered. A wrong code counts as an attempt; after
    /// <see cref="MaxAttempts"/> wrong ones the access code is locked, even for the right code.
    /// </summary>
    public AccessCodeVerification Verify(string? code, DateTime nowUtc)
    {
        Guard.Utc(nowUtc, "AccessCode.TimeMustBeUtc");

        if (UsedAtUtc is not null)
        {
            return AccessCodeVerification.AlreadyUsed;
        }

        if (IsExpired(nowUtc))
        {
            return AccessCodeVerification.Expired;
        }

        if (Attempts >= MaxAttempts)
        {
            return AccessCodeVerification.TooManyAttempts;
        }

        if (!Matches(code))
        {
            Attempts++;

            return AccessCodeVerification.Invalid;
        }

        UsedAtUtc = nowUtc;

        return AccessCodeVerification.Verified;
    }

    private static void EnsureCanIssue(List<DateTime> recentCreationTimesUtc, DateTime nowUtc)
    {
        if (recentCreationTimesUtc.Any(created => created.Kind != DateTimeKind.Utc))
        {
            throw new DomainException("AccessCode.TimeMustBeUtc");
        }

        if (recentCreationTimesUtc.Any(created => nowUtc < created + MinimumResendInterval))
        {
            throw new DomainException("AccessCode.ResendTooSoon");
        }

        if (recentCreationTimesUtc.Count(created => created > nowUtc - ResendWindow) >= MaxCodesPerHour)
        {
            throw new DomainException("AccessCode.ResendLimitReached");
        }
    }

    private bool Matches(string? code)
    {
        var candidate = code?.Trim();

        if (candidate is null || candidate.Length != CodeLength || !candidate.All(char.IsAsciiDigit))
        {
            return false;
        }

        // Constant time, so the time taken does not reveal how much of the hash matched.
        return CryptographicOperations.FixedTimeEquals(
            ComputeHash(Id, candidate),
            Convert.FromHexString(CodeHash));
    }

    private static byte[] ComputeHash(Guid salt, string code) =>
        SHA256.HashData(Encoding.UTF8.GetBytes($"{salt:N}:{code}"));
}