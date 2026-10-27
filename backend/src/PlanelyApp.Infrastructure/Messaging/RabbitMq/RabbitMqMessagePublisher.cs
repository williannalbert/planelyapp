using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace PlanelyApp.Infrastructure.Messaging.RabbitMq;

/// <summary>
/// Publishes with publisher confirms: the call returns only after the broker took the message, and throws if it
/// refused. One connection and one channel, opened on the first message and reopened after any failure.
/// Automatic recovery is off on purpose: the outbox is the retry, so a dropped connection just makes the next
/// pass open a new one, with the exchanges declared again.
/// </summary>
internal sealed class RabbitMqMessagePublisher(string connectionString, ILogger<RabbitMqMessagePublisher> logger)
    : IMessagePublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IConnection? _connection;
    private IChannel? _channel;

    public async Task PublishAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            var channel = await GetChannelAsync(cancellationToken);

            var properties = new BasicProperties
            {
                MessageId = message.MessageId.ToString(),
                Type = message.RoutingKey,
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
            };

            // With confirmation tracking on, this returns when the broker acknowledged the message.
            // "mandatory" is false: the monolith does not know who listens, and a message nobody asked for is not an error.
            await channel.BasicPublishAsync(
                message.Exchange, message.RoutingKey, false, properties, message.Body, cancellationToken);
        }
        catch
        {
            // After any failure the state of the channel is unknown: start over next time.
            await DiscardAsync();
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DiscardAsync();
        _gate.Dispose();
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true } open)
        {
            return open;
        }

        await DiscardAsync();

        var factory = new ConnectionFactory
        {
            Uri = new Uri(connectionString),
            ClientProvidedName = "planelyapp-outbox",
            AutomaticRecoveryEnabled = false,
        };

        IConnection? connection = null;
        IChannel? channel = null;

        try
        {
            connection = await factory.CreateConnectionAsync(cancellationToken);
            channel = await connection.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                cancellationToken);

            // Declaring is idempotent when the properties match, so it is safe on every (re)connection.
            foreach (var exchange in MessageTopology.Exchanges)
            {
                await channel.ExchangeDeclareAsync(
                    exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
            }
        }
        catch
        {
            if (channel is not null)
            {
                await channel.DisposeAsync();
            }

            if (connection is not null)
            {
                await connection.DisposeAsync();
            }

            throw;
        }

        _connection = connection;
        _channel = channel;

        logger.LogInformation("Connected to RabbitMQ; {Count} exchanges declared.", MessageTopology.Exchanges.Count);

        return channel;
    }

    private async Task DiscardAsync()
    {
        var channel = _channel;
        var connection = _connection;

        _channel = null;
        _connection = null;

        try
        {
            if (channel is not null)
            {
                await channel.DisposeAsync();
            }

            if (connection is not null)
            {
                await connection.DisposeAsync();
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Closing the RabbitMQ connection failed; it is dropped anyway.");
        }
    }
}