using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Messaging;

[Collection(OutboxCollection.Name)]
public class OutboxPublisherServiceTests(MySqlFixture mysql)
{
    [Fact]
    public async Task The_service_should_publish_what_is_added_while_it_runs()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await OutboxData.ClearAsync(mysql, cancellationToken);
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        var publisher = new FakeMessagePublisher();
        await using var provider = OutboxHost.Create(
            mysql, publisher, options => options.PollingInterval = TimeSpan.FromMilliseconds(50));
        var service = provider.GetServices<IHostedService>().Single();

        await service.StartAsync(cancellationToken);

        try
        {
            await OutboxData.AddAsync(mysql, company.Id, "customer.registered", OutboxData.Start, cancellationToken);
            await Eventually.UntilAsync(() => publisher.Published.Count == 1, cancellationToken);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        publisher.Published.Single().RoutingKey.ShouldBe("customer.registered");
        (await OutboxData.AllAsync(mysql, cancellationToken)).Single().ProcessedAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task The_service_should_survive_failures_and_publish_when_the_broker_returns()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await OutboxData.ClearAsync(mysql, cancellationToken);
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        await OutboxData.AddAsync(mysql, company.Id, "customer.updated", OutboxData.Start, cancellationToken);

        var calls = 0;
        var publisher = new FakeMessagePublisher
        {
            OnPublish = _ => Interlocked.Increment(ref calls) <= 2 ? new IOException("The broker is down") : null,
        };

        await using var provider = OutboxHost.Create(
            mysql,
            publisher,
            options =>
            {
                options.PollingInterval = TimeSpan.FromMilliseconds(20);
                options.MaxBackoff = TimeSpan.FromMilliseconds(100);
            });
        var service = provider.GetServices<IHostedService>().Single();

        await service.StartAsync(cancellationToken);

        try
        {
            await Eventually.UntilAsync(() => publisher.Published.Count == 1, cancellationToken);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        var row = (await OutboxData.AllAsync(mysql, cancellationToken)).Single();

        row.ProcessedAtUtc.ShouldNotBeNull();
        row.Attempts.ShouldBe(2);
    }
}