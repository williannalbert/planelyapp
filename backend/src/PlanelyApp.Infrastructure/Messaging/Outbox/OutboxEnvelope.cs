using System.Text.Json;

namespace PlanelyApp.Infrastructure.Messaging.Outbox;

/// <summary>
/// Builds the JSON every consumer receives: who, what and when around the content. The contract is this JSON,
/// not any class, because the consumer is another system with its own code.
/// </summary>
internal static class OutboxEnvelope
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static byte[] Serialize(OutboxMessage message)
    {
        // The document must stay alive while the envelope is written: "data" points into it.
        using var data = JsonDocument.Parse(message.Payload);

        var envelope = new Envelope(
            message.Id, message.Type, message.Version, message.OccurredAtUtc, message.CompanyId, data.RootElement);

        return JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
    }

    private sealed record Envelope(
        Guid MessageId,
        string Type,
        int Version,
        DateTime OccurredAt,
        Guid CompanyId,
        JsonElement Data);
}