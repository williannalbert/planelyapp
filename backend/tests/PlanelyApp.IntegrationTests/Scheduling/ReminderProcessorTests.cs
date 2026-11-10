using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PlanelyApp.Application.Scheduling.Appointments.Cancel;
using PlanelyApp.Application.Scheduling.Appointments.Confirm;
using PlanelyApp.Application.Scheduling.Appointments.Reschedule;
using PlanelyApp.Infrastructure;
using PlanelyApp.Infrastructure.Messaging.Outbox;
using PlanelyApp.Infrastructure.Reminders;
using PlanelyApp.IntegrationTests.Messaging;
using PlanelyApp.IntegrationTests.Persistence.Support;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Scheduling;

[Collection(OutboxCollection.Name)]
public class ReminderProcessorTests(MySqlFixture mysql)
{
    // Wide on purpose: the appointments of the tests are some days ahead.
    private static readonly ReminderOptions Wide = new() { LeadTime = TimeSpan.FromDays(60), BatchSize = 5000 };

    [Fact]
    public async Task A_confirmed_appointment_should_be_reminded_once_with_a_thin_message()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var id = await ConfirmedAsync(booking, BookingData.At(10), cancellationToken);

        var first = await NewProcessor().RunAsync(cancellationToken);
        var second = await NewProcessor().RunAsync(cancellationToken);

        first.Failed.ShouldBe(0);
        second.Failed.ShouldBe(0);
        var sentAt = await MarkAsync(booking, id, cancellationToken);
        sentAt.HasValue.ShouldBeTrue();
        sentAt!.Value.Kind.ShouldBe(DateTimeKind.Utc);
        sentAt.Value.ShouldBe(DateTime.UtcNow, TimeSpan.FromMinutes(5));

        var message = (await RemindersAsync(booking, cancellationToken)).Single();
        message.Version.ShouldBe(1);
        message.CompanyId.ShouldBe(booking.Company);
        message.ProcessedAtUtc.ShouldBeNull();
        using var data = JsonDocument.Parse(message.Payload);
        data.RootElement.EnumerateObject().Select(property => property.Name).ToList()
            .ShouldBe(["appointmentId", "branchId", "customerId", "professionalId", "startUtc"], ignoreOrder: true);
        data.RootElement.GetProperty("appointmentId").GetGuid().ShouldBe(id);
        data.RootElement.GetProperty("customerId").GetGuid().ShouldBe(booking.Customer);
        data.RootElement.GetProperty("startUtc").GetDateTime().ToUniversalTime().ShouldBe(BookingData.At(10));
    }

    [Fact]
    public async Task A_pending_appointment_should_be_reminded_only_after_it_is_confirmed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var id = await mysql.BookAsync(booking, BookingData.At(10), cancellationToken); // pending: the policy asks for it

        await NewProcessor().RunAsync(cancellationToken);

        (await MarkAsync(booking, id, cancellationToken)).ShouldBeNull();
        (await RemindersAsync(booking, cancellationToken)).ShouldBeEmpty();

        await ConfirmAsync(booking, id, cancellationToken);
        await NewProcessor().RunAsync(cancellationToken);

        (await MarkAsync(booking, id, cancellationToken)).HasValue.ShouldBeTrue();
        (await RemindersAsync(booking, cancellationToken)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task An_appointment_should_be_reminded_only_when_it_enters_the_window()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var id = await ConfirmedAsync(booking, BookingData.At(10), cancellationToken);
        var clock = new FakeClock(new DateTimeOffset(BookingData.At(10).AddHours(-25)));
        var processor = NewProcessor(new ReminderOptions(), clock); // 24 hours

        await processor.RunAsync(cancellationToken);

        (await MarkAsync(booking, id, cancellationToken)).ShouldBeNull();

        clock.Advance(TimeSpan.FromHours(2)); // 23 hours before the start
        await processor.RunAsync(cancellationToken);

        (await MarkAsync(booking, id, cancellationToken)).HasValue.ShouldBeTrue();
    }

    [Fact]
    public async Task A_cancelled_appointment_should_not_be_reminded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var id = await ConfirmedAsync(booking, BookingData.At(10), cancellationToken);
        (await mysql.SendAsStaffAsync(
            booking.Company, booking.Staff, new CancelAppointmentCommand(id), cancellationToken)).IsSuccess.ShouldBeTrue();

        await NewProcessor().RunAsync(cancellationToken);

        (await MarkAsync(booking, id, cancellationToken)).ShouldBeNull();
        (await RemindersAsync(booking, cancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Rescheduling_should_open_a_new_cycle_with_a_second_reminder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var id = await ConfirmedAsync(booking, BookingData.At(10), cancellationToken);
        await NewProcessor().RunAsync(cancellationToken);

        var moved = await mysql.SendAsStaffAsync(
            booking.Company,
            booking.Staff,
            new RescheduleAppointmentCommand(id, BookingData.At(14)),
            cancellationToken);

        moved.IsSuccess.ShouldBeTrue();
        (await MarkAsync(booking, id, cancellationToken)).ShouldBeNull();

        await NewProcessor().RunAsync(cancellationToken);

        var reminders = await RemindersAsync(booking, cancellationToken);
        reminders.Count.ShouldBe(2);
        reminders.Select(message => StartOf(message)).ToList()
            .ShouldBe([BookingData.At(10), BookingData.At(14)], ignoreOrder: true);
    }

    [Fact]
    public async Task Two_runs_at_the_same_time_should_remind_only_once()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var id = await ConfirmedAsync(booking, BookingData.At(10), cancellationToken);

        await Task.WhenAll(
            NewProcessor().RunAsync(cancellationToken),
            NewProcessor().RunAsync(cancellationToken));

        (await MarkAsync(booking, id, cancellationToken)).HasValue.ShouldBeTrue();
        (await RemindersAsync(booking, cancellationToken)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task One_run_should_remind_the_appointments_of_every_company_each_as_itself()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var first = await BookingData.SeedAsync(mysql, cancellationToken);
        var second = await BookingData.SeedAsync(mysql, cancellationToken);
        await ConfirmedAsync(first, BookingData.At(10), cancellationToken);
        await ConfirmedAsync(second, BookingData.At(11), cancellationToken);

        await NewProcessor().RunAsync(cancellationToken);

        foreach (var booking in new[] { first, second })
        {
            var message = (await RemindersAsync(booking, cancellationToken)).Single();
            message.CompanyId.ShouldBe(booking.Company);
            using var data = JsonDocument.Parse(message.Payload);
            data.RootElement.GetProperty("customerId").GetGuid().ShouldBe(booking.Customer);
        }
    }

    [Theory]
    [InlineData(0, 24, 1)]
    [InlineData(10, 0, 1)]
    [InlineData(10, 24, 0)]
    public void Invalid_options_should_be_refused(int batchSize, int leadTimeHours, int runEveryMinutes) =>
    Should.Throw<ArgumentOutOfRangeException>(() => new ServiceCollection().AddReminderProcessing(options =>
    {
        options.BatchSize = batchSize;
        options.LeadTime = TimeSpan.FromHours(leadTimeHours);
        options.RunEvery = TimeSpan.FromMinutes(runEveryMinutes);
    }));

    private ReminderProcessor NewProcessor(ReminderOptions? options = null, TimeProvider? clock = null)
    {
        using var scope = mysql.CreateScope();

        return new ReminderProcessor(
            scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            clock ?? TimeProvider.System,
            options ?? Wide,
            NullLogger<ReminderProcessor>.Instance);
    }

    private async Task<Guid> ConfirmedAsync(Booking booking, DateTime startUtc, CancellationToken cancellationToken)
    {
        var id = await mysql.BookAsync(booking, startUtc, cancellationToken);
        await ConfirmAsync(booking, id, cancellationToken);

        return id;
    }

    private async Task ConfirmAsync(Booking booking, Guid id, CancellationToken cancellationToken) =>
        (await mysql.SendAsStaffAsync(
            booking.Company, booking.Staff, new ConfirmAppointmentCommand(id), cancellationToken))
            .IsSuccess.ShouldBeTrue();

    private async Task<DateTime?> MarkAsync(Booking booking, Guid id, CancellationToken cancellationToken) =>
        (await mysql.LoadAppointmentAsync(booking.Company, id, cancellationToken))!.ReminderSentAtUtc;

    /// <summary>The table is shared, so a test only looks at the company it created.</summary>
    private async Task<List<OutboxMessage>> RemindersAsync(Booking booking, CancellationToken cancellationToken) =>
        (await OutboxData.ForCompanyAsync(mysql, booking.Company, cancellationToken))
            .Where(message => message.Type == "appointment.reminder_due")
            .ToList();

    private static DateTime StartOf(OutboxMessage message)
    {
        using var data = JsonDocument.Parse(message.Payload);

        return data.RootElement.GetProperty("startUtc").GetDateTime().ToUniversalTime();
    }
}