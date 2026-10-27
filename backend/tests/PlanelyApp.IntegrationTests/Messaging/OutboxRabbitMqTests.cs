using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Infrastructure;
using PlanelyApp.Infrastructure.Messaging;
using PlanelyApp.Infrastructure.Messaging.Outbox;
using PlanelyApp.IntegrationTests.Organization;
using RabbitMQ.Client;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Messaging;

[Collection(OutboxCollection.Name)]
public class OutboxRabbitMqTests(MySqlFixture mysql, RabbitMqFixture rabbit)
{
    private static readonly DateTime Start = OutboxData.Start;

    private ServiceProvider CreateProvider() =>
        OutboxHost.Create(mysql, services => services.AddRabbitMqPublisher(rabbit.ConnectionString));

    [Fact]
    public async Task A_message_in_the_outbox_should_reach_a_bound_queue_with_its_properties()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await OutboxData.ClearAsync(mysql, cancellationToken);
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var appointmentId = Guid.NewGuid();

        await using var probe = await RabbitMqProbe.ConnectAsync(rabbit.ConnectionString, cancellationToken);
        var queue = await probe.BindNewQueueAsync(MessageTopology.SchedulingExchange, "appointment.*", cancellationToken);

        await OutboxData.AddAsync(
            mysql, company.Id, "appointment.confirmed", Start, cancellationToken,
            data: new { appointmentId }, version: 2);

        await using var provider = CreateProvider();

        (await OutboxHost.RunOnceAsync(provider, cancellationToken)).ShouldBe(new OutboxRunResult(1, false));

        var row = (await OutboxData.AllAsync(mysql, cancellationToken)).Single();
        var received = (await probe.TakeAllAsync(queue, cancellationToken)).ShouldHaveSingleItem();

        received.MessageId.ShouldBe(row.Id.ToString());
        received.Type.ShouldBe("appointment.confirmed");
        received.ContentType.ShouldBe("application/json");
        received.DeliveryMode.ShouldBe(DeliveryModes.Persistent);

        using var json = JsonDocument.Parse(received.Body);
        json.RootElement.GetProperty("messageId").GetGuid().ShouldBe(row.Id);
        json.RootElement.GetProperty("version").GetInt32().ShouldBe(2);
        json.RootElement.GetProperty("companyId").GetGuid().ShouldBe(company.Id);
        json.RootElement.GetProperty("data").GetProperty("appointmentId").GetGuid().ShouldBe(appointmentId);

        row.ProcessedAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task Each_message_should_go_to_the_exchange_of_its_subject()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await OutboxData.ClearAsync(mysql, cancellationToken);
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        await using var probe = await RabbitMqProbe.ConnectAsync(rabbit.ConnectionString, cancellationToken);
        var scheduling = await probe.BindNewQueueAsync(MessageTopology.SchedulingExchange, "#", cancellationToken);
        var catalog = await probe.BindNewQueueAsync(MessageTopology.CatalogExchange, "#", cancellationToken);
        var organization = await probe.BindNewQueueAsync(MessageTopology.OrganizationExchange, "#", cancellationToken);

        await OutboxData.AddAsync(mysql, company.Id, "appointment.created", Start, cancellationToken);
        await OutboxData.AddAsync(mysql, company.Id, "promotion.created", Start.AddMinutes(1), cancellationToken);
        await OutboxData.AddAsync(mysql, company.Id, "customer.registered", Start.AddMinutes(2), cancellationToken);

        await using var provider = CreateProvider();

        (await OutboxHost.RunOnceAsync(provider, cancellationToken)).Published.ShouldBe(3);

        (await probe.TakeAllAsync(scheduling, cancellationToken)).Select(message => message.Type).ShouldBe(["appointment.created"]);
        (await probe.TakeAllAsync(catalog, cancellationToken)).Select(message => message.Type).ShouldBe(["promotion.created"]);
        (await probe.TakeAllAsync(organization, cancellationToken)).Select(message => message.Type).ShouldBe(["customer.registered"]);
    }

    [Fact]
    public async Task A_queue_should_only_receive_what_its_binding_asks_for()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await OutboxData.ClearAsync(mysql, cancellationToken);
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        await using var probe = await RabbitMqProbe.ConnectAsync(rabbit.ConnectionString, cancellationToken);
        var everything = await probe.BindNewQueueAsync(MessageTopology.SchedulingExchange, "appointment.*", cancellationToken);
        var onlyCancelled = await probe.BindNewQueueAsync(MessageTopology.SchedulingExchange, "appointment.cancelled", cancellationToken);

        await OutboxData.AddAsync(mysql, company.Id, "appointment.created", Start, cancellationToken);
        await OutboxData.AddAsync(mysql, company.Id, "appointment.cancelled", Start.AddMinutes(1), cancellationToken);

        await using var provider = CreateProvider();

        await OutboxHost.RunOnceAsync(provider, cancellationToken);

        (await probe.TakeAllAsync(everything, cancellationToken)).Count.ShouldBe(2);
        (await probe.TakeAllAsync(onlyCancelled, cancellationToken)).Select(message => message.Type).ShouldBe(["appointment.cancelled"]);
    }

    [Fact]
    public async Task A_message_nobody_is_bound_to_should_be_confirmed_and_lost()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await OutboxData.ClearAsync(mysql, cancellationToken);
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        await OutboxData.AddAsync(mysql, company.Id, "promotion.created", Start, cancellationToken);

        await using var provider = CreateProvider();

        // The broker accepts it: an exchange with no queue bound is not an error, the message just goes nowhere.
        (await OutboxHost.RunOnceAsync(provider, cancellationToken)).ShouldBe(new OutboxRunResult(1, false));
        (await OutboxData.AllAsync(mysql, cancellationToken)).Single().ProcessedAtUtc.ShouldNotBeNull();

        // A consumer that binds afterwards does not get it.
        await using var probe = await RabbitMqProbe.ConnectAsync(rabbit.ConnectionString, cancellationToken);
        var late = await probe.BindNewQueueAsync(MessageTopology.CatalogExchange, "#", cancellationToken);

        (await probe.TakeAllAsync(late, cancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_publisher_should_reconnect_after_the_connection_is_dropped()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await OutboxData.ClearAsync(mysql, cancellationToken);
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        // One provider = one publisher = the same connection reused across passes.
        await using var provider = CreateProvider();

        await using (var before = await RabbitMqProbe.ConnectAsync(rabbit.ConnectionString, cancellationToken))
        {
            var queue = await before.BindNewQueueAsync(MessageTopology.OrganizationExchange, "#", cancellationToken);

            await OutboxData.AddAsync(mysql, company.Id, "customer.registered", Start, cancellationToken);
            (await OutboxHost.RunOnceAsync(provider, cancellationToken)).Published.ShouldBe(1);
            (await before.TakeAllAsync(queue, cancellationToken)).Count.ShouldBe(1);
        }

        (await rabbit.CloseAllConnectionsAsync(cancellationToken)).ShouldBe(0);

        await using var after = await RabbitMqProbe.ConnectAsync(rabbit.ConnectionString, cancellationToken);
        var newQueue = await after.BindNewQueueAsync(MessageTopology.OrganizationExchange, "#", cancellationToken);

        await OutboxData.AddAsync(mysql, company.Id, "customer.updated", Start.AddMinutes(1), cancellationToken);

        // The first pass may still find the old channel "open" and fail; the outbox keeps the message and the
        // next pass opens a new connection. That is the retry the design relies on.
        var published = 0;

        for (var pass = 0; pass < 10 && published == 0; pass++)
        {
            published = (await OutboxHost.RunOnceAsync(provider, cancellationToken)).Published;

            if (published == 0)
            {
                await Task.Delay(300, cancellationToken);
            }
        }

        published.ShouldBe(1);

        var received = (await after.TakeAllAsync(newQueue, cancellationToken)).ShouldHaveSingleItem();
        received.Type.ShouldBe("customer.updated");
    }
}