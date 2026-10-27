using Testcontainers.RabbitMq;

namespace PlanelyApp.IntegrationTests.Messaging;

/// <summary>One disposable RabbitMQ, the same line as the docker-compose, shared by the outbox tests.</summary>
public sealed class RabbitMqFixture : IAsyncLifetime
{
    private readonly RabbitMqContainer _container = new RabbitMqBuilder("rabbitmq:4-management").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    /// <summary>Drops every client connection, as a broker restart or a network failure would. Returns the exit code.</summary>
    public async Task<long> CloseAllConnectionsAsync(CancellationToken cancellationToken)
    {
        var result = await _container.ExecAsync(["rabbitmqctl", "close_all_connections", "test"], cancellationToken);

        return result.ExitCode ?? -1;
    }
}

/// <summary>
/// The outbox tests share one table, so they run one after the other, and they share one RabbitMQ.
/// A class that is not in this collection must not touch outbox_messages.
/// </summary>
[CollectionDefinition(Name)]
public sealed class OutboxCollection : ICollectionFixture<RabbitMqFixture>
{
    public const string Name = "Outbox";
}