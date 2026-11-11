using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using PlanelyApp.Application;
using PlanelyApp.Application.Scheduling.Appointments.Confirm;
using PlanelyApp.Infrastructure;
using PlanelyApp.Infrastructure.Reminders;
using PlanelyApp.IntegrationTests.Messaging;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Scheduling;

[Collection(OutboxCollection.Name)]
public class ReminderSchedulingTests(MySqlFixture mysql)
{
    [Fact]
    public async Task The_scheduler_should_remind_what_is_due_at_start_and_what_becomes_due_later()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var first = await BookingData.SeedAsync(mysql, cancellationToken);
        var firstId = await ConfirmedAsync(first, cancellationToken);

        await using var provider = CreateProvider();
        var services = provider.GetServices<IHostedService>().ToList();

        foreach (var service in services)
        {
            await service.StartAsync(cancellationToken);
        }

        try
        {
            await Eventually.UntilAsync(() => IsRemindedAsync(first, firstId, cancellationToken), cancellationToken);

            // Confirmed while the scheduler is already running: a later run takes it.
            var second = await BookingData.SeedAsync(mysql, cancellationToken);
            var secondId = await ConfirmedAsync(second, cancellationToken);

            await Eventually.UntilAsync(() => IsRemindedAsync(second, secondId, cancellationToken), cancellationToken);
        }
        finally
        {
            foreach (var service in Enumerable.Reverse(services))
            {
                await service.StopAsync(CancellationToken.None);
            }
        }
    }

    [Fact]
    public async Task A_failure_in_a_run_should_be_logged_and_not_thrown_to_the_scheduler()
    {
        // Nothing is registered, so the processor fails as it would with the database down.
        await using var empty = new ServiceCollection().BuildServiceProvider();
        var processor = new ReminderProcessor(
            empty.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            new ReminderOptions(),
            NullLogger<ReminderProcessor>.Instance);
        var job = new SendRemindersJob(processor, NullLogger<SendRemindersJob>.Instance);

        await Should.NotThrowAsync(async () => await job.Execute(null!, TestContext.Current.CancellationToken));
    }

    private ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();

        // The host the Api runs in says when the application has started. Here it has, from the start.
        services.AddSingleton<IHostApplicationLifetime, StartedLifetime>();
        services.AddInfrastructure(mysql.ConnectionString);
        services.AddApplication();
        services.AddReminderScheduling(options =>
        {
            options.LeadTime = TimeSpan.FromDays(60); // the appointments of the tests are some days ahead
            options.BatchSize = 5000;
            options.RunEvery = TimeSpan.FromSeconds(1);
        });

        return services.BuildServiceProvider();
    }

    private async Task<Guid> ConfirmedAsync(Booking booking, CancellationToken cancellationToken)
    {
        var id = await mysql.BookAsync(booking, BookingData.At(10), cancellationToken);
        (await mysql.SendAsStaffAsync(
            booking.Company, booking.Staff, new ConfirmAppointmentCommand(id), cancellationToken))
            .IsSuccess.ShouldBeTrue();

        return id;
    }

    private async Task<bool> IsRemindedAsync(Booking booking, Guid id, CancellationToken cancellationToken) =>
        (await mysql.LoadAppointmentAsync(booking.Company, id, cancellationToken))!.ReminderSentAtUtc is not null;

    private sealed class StartedLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => new(true);

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
        }
    }
}