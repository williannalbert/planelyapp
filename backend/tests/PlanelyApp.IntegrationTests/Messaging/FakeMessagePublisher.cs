using PlanelyApp.Infrastructure.Messaging;

namespace PlanelyApp.IntegrationTests.Messaging;

/// <summary>Stands in for the broker: remembers what was published, and fails when the test says so.</summary>
internal sealed class FakeMessagePublisher : IMessagePublisher
{
    private readonly List<OutboundMessage> _published = [];

    public IReadOnlyList<OutboundMessage> Published
    {
        get
        {
            lock (_published)
            {
                return _published.ToArray();
            }
        }
    }

    /// <summary>Returns the exception to throw for that message, or null to accept it.</summary>
    public Func<OutboundMessage, Exception?> OnPublish { get; set; } = _ => null;

    public Task PublishAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        if (OnPublish(message) is { } failure)
        {
            throw failure;
        }

        lock (_published)
        {
            _published.Add(message);
        }

        return Task.CompletedTask;
    }
}