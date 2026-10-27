namespace PlanelyApp.Infrastructure.Messaging;

/// <summary>A message ready for the wire: where it goes and the bytes to send. Nothing here is specific to RabbitMQ.</summary>
public sealed record OutboundMessage(Guid MessageId, string Exchange, string RoutingKey, ReadOnlyMemory<byte> Body);

public interface IMessagePublisher
{
    /// <summary>Returns only when the broker took responsibility for the message. Throws if it did not.</summary>
    Task PublishAsync(OutboundMessage message, CancellationToken cancellationToken = default);
}