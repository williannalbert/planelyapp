using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Infrastructure.Messaging;
using PlanelyApp.Infrastructure.Messaging.Outbox;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Messaging;

[Collection(OutboxCollection.Name)]
public class OutboxProcessorTests(MySqlFixture mysql)
{
    private static readonly DateTime Start = OutboxData.Start;

    [Fact]
    public async Task Pending_messages_should_be_published_in_the_order_they_happened()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await OutboxData.ClearAsync(mysql, cancellationToken);
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        // Saved out of order on purpose: publication follows the order of occurrence, not of insertion.
        await OutboxData.AddAsync(mysql, company.Id, "appointment.cancelled", Start.AddMinutes(2), cancellationToken);
        await OutboxData.AddAsync(mysql, company.Id, "appointment.created", Start, cancellationToken);
        await OutboxData.AddAsync(mysql, company.Id, "appointment.confirmed", Start.AddMinutes(1), cancellationToken);

        var publisher = new FakeMessagePublisher();
        await using var provider = OutboxHost.Create(mysql, publisher);
        var before = DateTime.UtcNow;

        var result = await OutboxHost.RunOnceAsync(provider, cancellationToken);

        result.ShouldBe(new OutboxRunResult(3, false));
        publisher.Published.Select(message => message.RoutingKey)
            .ShouldBe(["appointment.created", "appointment.confirmed", "appointment.cancelled"]);
        publisher.Published.ShouldAllBe(message => message.Exchange == MessageTopology.SchedulingExchange);

        var rows = await OutboxData.AllAsync(mysql, cancellationToken);

        rows.ShouldAllBe(row => row.ProcessedAtUtc != null && row.Attempts == 0 && row.Error == null);
        rows.ShouldAllBe(row => row.ProcessedAtUtc!.Value >= before);
        rows.ShouldAllBe(row => row.ProcessedAtUtc!.Value.Kind == DateTimeKind.Utc);
    }

    [Fact]
    public async Task The_message_on_the_wire_should_carry_the_envelope_and_the_content()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await OutboxData.ClearAsync(mysql, cancellationToken);
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var appointmentId = Guid.NewGuid();

        await OutboxData.AddAsync(
            mysql, company.Id, "appointment.confirmed", Start, cancellationToken,
            data: new { appointmentId, total = 850.00m, service = "Tatuagem à mão" }, version: 3);

        var publisher = new FakeMessagePublisher();
        await using var provider = OutboxHost.Create(mysql, publisher);

        await OutboxHost.RunOnceAsync(provider, cancellationToken);

        var row = (await OutboxData.AllAsync(mysql, cancellationToken)).Single();
        var message = publisher.Published.Single();

        message.MessageId.ShouldBe(row.Id);
        message.RoutingKey.ShouldBe("appointment.confirmed");

        using var json = JsonDocument.Parse(message.Body);
        var root = json.RootElement;

        root.GetProperty("messageId").GetGuid().ShouldBe(row.Id);
        root.GetProperty("type").GetString().ShouldBe("appointment.confirmed");
        root.GetProperty("version").GetInt32().ShouldBe(3);
        root.GetProperty("occurredAt").GetDateTime().ShouldBe(Start);
        root.GetProperty("companyId").GetGuid().ShouldBe(company.Id);

        var data = root.GetProperty("data");
        data.GetProperty("appointmentId").GetGuid().ShouldBe(appointmentId);
        data.GetProperty("total").GetDecimal().ShouldBe(850.00m);
        data.GetProperty("service").GetString().ShouldBe("Tatuagem à mão");
    }

    [Fact]
    public async Task Messages_already_published_should_not_be_published_again()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await OutboxData.ClearAsync(mysql, cancellationToken);
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        await OutboxData.AddAsync(mysql, company.Id, "customer.registered", Start, cancellationToken);

        var publisher = new FakeMessagePublisher();
        await using var provider = OutboxHost.Create(mysql, publisher);

        (await OutboxHost.RunOnceAsync(provider, cancellationToken)).Published.ShouldBe(1);
        (await OutboxHost.RunOnceAsync(provider, cancellationToken)).ShouldBe(new OutboxRunResult(0, false));

        publisher.Published.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Messages_of_every_company_should_be_published_by_a_scope_without_company()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await OutboxData.ClearAsync(mysql, cancellationToken);
        var first = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var second = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        await OutboxData.AddAsync(mysql, first.Id, "customer.registered", Start, cancellationToken);
        await OutboxData.AddAsync(mysql, second.Id, "customer.registered", Start.AddMinutes(1), cancellationToken);

        var publisher = new FakeMessagePublisher();
        await using var provider = OutboxHost.Create(mysql, publisher);

        (await OutboxHost.RunOnceAsync(provider, cancellationToken)).Published.ShouldBe(2);

        var companies = publisher.Published
            .Select(message =>
            {
                using var json = JsonDocument.Parse(message.Body);
                return json.RootElement.GetProperty("companyId").GetGuid();
            })
            .ToList();

        companies.ShouldBe([first.Id, second.Id]);
    }

    [Fact]
    public async Task A_failure_should_stop_the_pass_keep_the_order_and_count_the_attempt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await OutboxData.ClearAsync(mysql, cancellationToken);
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        await OutboxData.AddAsync(mysql, company.Id, "appointment.created", Start, cancellationToken);
        await OutboxData.AddAsync(mysql, company.Id, "appointment.confirmed", Start.AddMinutes(1), cancellationToken);
        await OutboxData.AddAsync(mysql, company.Id, "appointment.cancelled", Start.AddMinutes(2), cancellationToken);

        var publisher = new FakeMessagePublisher
        {
            OnPublish = message => message.RoutingKey == "appointment.confirmed"
                ? new IOException("The broker is unavailable")
                : null,
        };
        await using var provider = OutboxHost.Create(mysql, publisher);

        var first = await OutboxHost.RunOnceAsync(provider, cancellationToken);

        first.ShouldBe(new OutboxRunResult(1, true));

        var afterFailure = await OutboxData.AllAsync(mysql, cancellationToken);

        afterFailure[0].ProcessedAtUtc.ShouldNotBeNull();
        afterFailure[1].ProcessedAtUtc.ShouldBeNull();
        afterFailure[1].Attempts.ShouldBe(1);
        afterFailure[1].Error.ShouldBe("The broker is unavailable");

        // The one after the failure was not touched: it would have arrived before the one that failed.
        afterFailure[2].ProcessedAtUtc.ShouldBeNull();
        afterFailure[2].Attempts.ShouldBe(0);
        publisher.Published.Select(message => message.RoutingKey).ShouldBe(["appointment.created"]);

        // The broker is back: the failed one goes first, then the one that waited.
        publisher.OnPublish = _ => null;

        (await OutboxHost.RunOnceAsync(provider, cancellationToken)).ShouldBe(new OutboxRunResult(2, false));

        publisher.Published.Select(message => message.RoutingKey)
            .ShouldBe(["appointment.created", "appointment.confirmed", "appointment.cancelled"]);

        var afterRecovery = await OutboxData.AllAsync(mysql, cancellationToken);

        afterRecovery.ShouldAllBe(row => row.ProcessedAtUtc != null);
        afterRecovery[1].Attempts.ShouldBe(1);
        afterRecovery[1].Error.ShouldBe("The broker is unavailable"); // history of the failure, kept
    }

    [Fact]
    public async Task A_very_long_error_should_be_cut_to_fit_the_column()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await OutboxData.ClearAsync(mysql, cancellationToken);
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        await OutboxData.AddAsync(mysql, company.Id, "customer.updated", Start, cancellationToken);

        var publisher = new FakeMessagePublisher
        {
            OnPublish = _ => new InvalidOperationException(new string('x', 5000)),
        };
        await using var provider = OutboxHost.Create(mysql, publisher);

        (await OutboxHost.RunOnceAsync(provider, cancellationToken)).Failed.ShouldBeTrue();

        (await OutboxData.AllAsync(mysql, cancellationToken)).Single().Error!.Length.ShouldBe(OutboxMessage.MaxErrorLength);
    }

    [Fact]
    public async Task The_batch_size_should_limit_each_pass()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await OutboxData.ClearAsync(mysql, cancellationToken);
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        for (var minute = 0; minute < 5; minute++)
        {
            await OutboxData.AddAsync(mysql, company.Id, "customer.updated", Start.AddMinutes(minute), cancellationToken);
        }

        var publisher = new FakeMessagePublisher();
        await using var provider = OutboxHost.Create(mysql, publisher, options => options.BatchSize = 2);

        var runs = new List<int>
        {
            (await OutboxHost.RunOnceAsync(provider, cancellationToken)).Published,
            (await OutboxHost.RunOnceAsync(provider, cancellationToken)).Published,
            (await OutboxHost.RunOnceAsync(provider, cancellationToken)).Published,
            (await OutboxHost.RunOnceAsync(provider, cancellationToken)).Published,
        };

        runs.ShouldBe([2, 2, 1, 0]);
        publisher.Published.Count.ShouldBe(5);
    }
}