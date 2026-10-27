namespace PlanelyApp.Infrastructure.Messaging.Outbox;

/// <summary>
/// One fact waiting to be published. Not an aggregate (nobody loads it to apply a business rule), so it has no
/// audit columns, and it has no tenant filter: the publisher serves every company.
/// </summary>
public sealed class OutboxMessage
{
    public const int MaxTypeLength = 100;
    public const int MaxErrorLength = 2000;

#pragma warning disable CS8618 // EF Core fills the properties
    private OutboxMessage()
    {
    }
#pragma warning restore CS8618

    public Guid Id { get; private set; }

    public string Type { get; private set; }

    public int Version { get; private set; }

    public Guid CompanyId { get; private set; }

    /// <summary>The "data" of the message, as JSON.</summary>
    public string Payload { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>Null while the message is pending.</summary>
    public DateTime? ProcessedAtUtc { get; private set; }

    /// <summary>How many times publishing it failed.</summary>
    public int Attempts { get; private set; }

    /// <summary>The last failure. Kept after the message is finally published, as history.</summary>
    public string? Error { get; private set; }

    public static OutboxMessage Create(string type, int version, Guid companyId, string payload, DateTime occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(type.Length, MaxTypeLength);
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);

        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Company id cannot be empty.", nameof(companyId));
        }

        if (occurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The instant must be in UTC.", nameof(occurredAtUtc));
        }

        return new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            Type = type,
            Version = version,
            CompanyId = companyId,
            Payload = payload,
            OccurredAtUtc = occurredAtUtc,
        };
    }

    public void MarkProcessed(DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The instant must be in UTC.", nameof(nowUtc));
        }

        ProcessedAtUtc = nowUtc;
    }

    public void RegisterFailure(string error)
    {
        Attempts++;
        Error = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
    }
}