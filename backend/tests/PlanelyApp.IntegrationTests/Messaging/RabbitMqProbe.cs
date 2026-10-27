using System.Text;
using PlanelyApp.Infrastructure.Messaging;
using RabbitMQ.Client;

namespace PlanelyApp.IntegrationTests.Messaging;

internal sealed record ReceivedMessage(
    string Body,
    string? MessageId,
    string? Type,
    string? ContentType,
    DeliveryModes? DeliveryMode);

/// <summary>
/// Plays the part of the consumer: owns a connection, binds queues to the exchanges and takes what arrived.
/// The queues are exclusive and die with the connection, like a consumer that is not there anymore.
/// </summary>
internal sealed class RabbitMqProbe : IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;

    private RabbitMqProbe(IConnection connection, IChannel channel)
    {
        _connection = connection;
        _channel = channel;
    }

    public static async Task<RabbitMqProbe> ConnectAsync(string connectionString, CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory { Uri = new Uri(connectionString) };

        var connection = await factory.CreateConnectionAsync(cancellationToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        // Same arguments as the publisher: if they differed, one of the two would fail with PRECONDITION_FAILED.
        foreach (var exchange in MessageTopology.Exchanges)
        {
            await channel.ExchangeDeclareAsync(
                exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        }

        return new RabbitMqProbe(connection, channel);
    }

    public async Task<string> BindNewQueueAsync(string exchange, string routingKey, CancellationToken cancellationToken)
    {
        var queue = await _channel.QueueDeclareAsync(
            queue: string.Empty, durable: false, exclusive: true, autoDelete: true, cancellationToken: cancellationToken);

        await _channel.QueueBindAsync(queue.QueueName, exchange, routingKey, cancellationToken: cancellationToken);

        return queue.QueueName;
    }

    public async Task<List<ReceivedMessage>> TakeAllAsync(string queue, CancellationToken cancellationToken)
    {
        var received = new List<ReceivedMessage>();

        while (await _channel.BasicGetAsync(queue, true, cancellationToken) is { } result)
        {
            received.Add(new ReceivedMessage(
                Encoding.UTF8.GetString(result.Body.ToArray()),
                result.BasicProperties.MessageId,
                result.BasicProperties.Type,
                result.BasicProperties.ContentType,
                result.BasicProperties.DeliveryMode));
        }

        return received;
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.DisposeAsync();
        await _connection.DisposeAsync();
    }
}