using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Infrastructure;
using PlanelyApp.Infrastructure.Messaging;
using PlanelyApp.Infrastructure.Messaging.Outbox;

namespace PlanelyApp.IntegrationTests.Messaging;

internal static class OutboxHost
{
    public static ServiceProvider Create(
        MySqlFixture mysql,
        FakeMessagePublisher publisher,
        Action<OutboxOptions>? configure = null) =>
        Create(mysql, services => services.AddSingleton<IMessagePublisher>(publisher), configure);

    /// <summary>The registration of the Api, over the test MySQL, with the publisher the test chooses. No company in the scope.</summary>
    public static ServiceProvider Create(
        MySqlFixture mysql,
        Action<IServiceCollection> addPublisher,
        Action<OutboxOptions>? configure = null)
    {
        var services = new ServiceCollection();

        services.AddInfrastructure(mysql.ConnectionString);
        services.AddOutboxProcessing(configure);
        addPublisher(services);

        return services.BuildServiceProvider();
    }

    public static async Task<OutboxRunResult> RunOnceAsync(IServiceProvider provider, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().ProcessPendingAsync(cancellationToken);
    }
}

internal static class Eventually
{
    public static async Task UntilAsync(Func<bool> condition, CancellationToken cancellationToken, int timeoutSeconds = 15)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The condition was not met in time.");
            }

            await Task.Delay(25, cancellationToken);
        }
    }
}