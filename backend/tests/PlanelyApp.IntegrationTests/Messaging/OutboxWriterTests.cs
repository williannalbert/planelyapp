using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Application.Abstractions.Messaging;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using PlanelyApp.Infrastructure.Messaging;
using PlanelyApp.Infrastructure.Messaging.Outbox;
using PlanelyApp.Infrastructure.Persistence;
using PlanelyApp.Infrastructure.Tenancy;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Messaging;

[Collection(OutboxCollection.Name)]
public class OutboxWriterTests(MySqlFixture mysql)
{
    private static readonly DateTime Now = OutboxData.Start;

    [Fact]
    public async Task A_message_added_and_saved_should_come_back_with_everything_filled()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await OutboxData.ClearAsync(mysql, cancellationToken);
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var appointmentId = Guid.NewGuid();

        await using (var scope = mysql.CreateScope(company.Id))
        {
            scope.ServiceProvider.GetRequiredService<IOutbox>().Add(
                "appointment.confirmed",
                company.Id,
                new { appointmentId, total = 850.00m, service = "Tatuagem à mão", day = DayOfWeek.Friday },
                Now,
                version: 2);

            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
        }

        var message = (await OutboxData.AllAsync(mysql, cancellationToken)).Single();

        message.Id.ShouldNotBe(Guid.Empty);
        message.Type.ShouldBe("appointment.confirmed");
        message.Version.ShouldBe(2);
        message.CompanyId.ShouldBe(company.Id);
        message.OccurredAtUtc.ShouldBe(Now);
        message.OccurredAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
        message.ProcessedAtUtc.ShouldBeNull();
        message.Attempts.ShouldBe(0);
        message.Error.ShouldBeNull();

        // The JSON column normalizes the text, so the test reads values, not the string.
        using var payload = JsonDocument.Parse(message.Payload);
        payload.RootElement.GetProperty("appointmentId").GetGuid().ShouldBe(appointmentId);
        payload.RootElement.GetProperty("total").GetDecimal().ShouldBe(850.00m);
        payload.RootElement.GetProperty("service").GetString().ShouldBe("Tatuagem à mão");
        payload.RootElement.GetProperty("day").GetString().ShouldBe("Friday");
    }

    [Fact]
    public async Task The_message_and_the_change_that_caused_it_should_be_saved_together_or_not_at_all()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await OutboxData.ClearAsync(mysql, cancellationToken);
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var email = Email.Create(StaffData.NewEmail());

        await mysql.SaveAsync(company.Id, cancellationToken,
            services => services.GetRequiredService<ICustomerRepository>().Add(StaffData.NewCustomer(company.Id, email)));

        await using (var scope = mysql.CreateScope(company.Id))
        {
            // The second customer repeats the e-mail of the first: the unique index refuses it.
            scope.ServiceProvider.GetRequiredService<ICustomerRepository>().Add(StaffData.NewCustomer(company.Id, email));
            scope.ServiceProvider.GetRequiredService<IOutbox>()
                .Add("customer.registered", company.Id, new { reason = "duplicate" }, Now);

            await Should.ThrowAsync<DbUpdateException>(
                () => scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken));
        }

        // The message of the change that failed was not left behind.
        (await OutboxData.AllAsync(mysql, cancellationToken)).ShouldBeEmpty();

        await using var check = mysql.CreateScope(company.Id);
        (await check.ServiceProvider.GetRequiredService<PlanelyDbContext>()
            .Set<Customer>().CountAsync(cancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task A_message_for_another_company_should_be_refused()
    {
        var company = Guid.NewGuid();
        var other = Guid.NewGuid();

        using var scope = mysql.CreateScope(company);

        Should.Throw<TenantViolationException>(
            () => scope.ServiceProvider.GetRequiredService<IOutbox>().Add("customer.updated", other, new { }, Now));

        await Task.CompletedTask;
    }

    [Fact]
    public async Task A_scope_without_company_may_add_a_message_for_any_company()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await OutboxData.ClearAsync(mysql, cancellationToken);
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        // The reminders job serves every company and runs with no company set.
        await using (var scope = mysql.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<IOutbox>()
                .Add("appointment.reminder", company.Id, new { appointmentId = Guid.NewGuid() }, Now);

            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
        }

        (await OutboxData.AllAsync(mysql, cancellationToken)).Single().CompanyId.ShouldBe(company.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("appointment")]
    [InlineData("appointment.")]
    [InlineData(".confirmed")]
    [InlineData("invoice.paid")]
    public void A_type_without_an_exchange_should_be_refused(string type)
    {
        var company = Guid.NewGuid();

        using var scope = mysql.CreateScope(company);

        Should.Throw<ArgumentException>(
            () => scope.ServiceProvider.GetRequiredService<IOutbox>().Add(type, company, new { }, Now));
    }

    [Fact]
    public void An_instant_that_is_not_utc_should_be_refused()
    {
        var company = Guid.NewGuid();
        var local = DateTime.SpecifyKind(Now, DateTimeKind.Unspecified);

        using var scope = mysql.CreateScope(company);

        Should.Throw<ArgumentException>(
            () => scope.ServiceProvider.GetRequiredService<IOutbox>().Add("customer.updated", company, new { }, local));
    }

    [Theory]
    [InlineData("appointment.created", MessageTopology.SchedulingExchange)]
    [InlineData("appointment.reminder", MessageTopology.SchedulingExchange)]
    [InlineData("promotion.created", MessageTopology.CatalogExchange)]
    [InlineData("customer.registered", MessageTopology.OrganizationExchange)]
    public void The_subject_of_the_type_should_pick_the_exchange(string type, string expectedExchange)
    {
        MessageTopology.TryGetExchange(type, out var exchange).ShouldBeTrue();
        exchange.ShouldBe(expectedExchange);
    }
}