using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Application.Scheduling.Appointments.Cancel;
using PlanelyApp.Application.Scheduling.Appointments.Confirm;
using PlanelyApp.Application.Scheduling.Appointments.Reschedule;
using PlanelyApp.Application.Scheduling.Appointments.SetItemPrice;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Infrastructure.Messaging.Outbox;
using PlanelyApp.IntegrationTests.Messaging;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Scheduling;

/// <summary>What the use cases leave in the outbox: in the same transaction as the change, and nothing when it fails.</summary>
public class AppointmentMessagesUseCaseTests(MySqlFixture mysql)
{
    [Fact]
    public async Task Booking_should_leave_a_created_message_in_the_same_transaction()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);

        var id = await mysql.BookAsync(booking, BookingData.At(10), cancellationToken);

        var message = (await MessagesAsync(booking, cancellationToken)).Single();
        message.Type.ShouldBe("appointment.created");
        message.Version.ShouldBe(1);
        message.CompanyId.ShouldBe(booking.Company);
        message.ProcessedAtUtc.ShouldBeNull();
        message.OccurredAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
        using var data = JsonDocument.Parse(message.Payload);
        var root = data.RootElement;
        root.GetProperty("appointmentId").GetGuid().ShouldBe(id);
        root.GetProperty("branchId").GetGuid().ShouldBe(booking.Centro);
        root.GetProperty("customerId").GetGuid().ShouldBe(booking.Customer);
        root.GetProperty("professionalId").GetGuid().ShouldBe(booking.Ana);
        root.GetProperty("startUtc").GetDateTime().ToUniversalTime().ShouldBe(BookingData.At(10));
        root.GetProperty("status").GetString().ShouldBe("Pending");
    }

    [Fact]
    public async Task Confirming_should_leave_a_confirmed_message_with_the_total_after_the_created_one()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var id = await mysql.BookAsync(booking, BookingData.At(10), cancellationToken);

        await mysql.SendAsStaffAsync(booking.Company, booking.Staff, new ConfirmAppointmentCommand(id), cancellationToken);

        var messages = await MessagesAsync(booking, cancellationToken);
        messages.Select(message => message.Type).ToList()
            .ShouldBe(["appointment.created", "appointment.confirmed"], ignoreOrder: false);
        using var data = JsonDocument.Parse(messages[1].Payload);
        data.RootElement.GetProperty("appointmentId").GetGuid().ShouldBe(id);
        data.RootElement.GetProperty("totalPrice").GetDecimal().ShouldBe(50m);
    }

    [Fact]
    public async Task Cancelling_as_the_staff_should_leave_a_message_without_the_reason()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var id = await mysql.BookAsync(booking, BookingData.At(10), cancellationToken);

        await mysql.SendAsStaffAsync(
            booking.Company, booking.Staff, new CancelAppointmentCommand(id, "Called me at (11) 99999-0000"), cancellationToken);

        var message = (await MessagesAsync(booking, cancellationToken)).Single(m => m.Type == "appointment.cancelled");
        using var data = JsonDocument.Parse(message.Payload);
        data.RootElement.GetProperty("appointmentId").GetGuid().ShouldBe(id);
        data.RootElement.GetProperty("cancelledByCustomer").GetBoolean().ShouldBeFalse();
        data.RootElement.TryGetProperty("reason", out _).ShouldBeFalse();
        message.Payload.ShouldNotContain("99999");
    }

    [Fact]
    public async Task Cancelling_as_the_customer_should_say_so()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var id = await mysql.BookAsync(booking, BookingData.At(10), cancellationToken);

        var result = await mysql.SendAsCustomerAsync(
            booking.Company, booking.Customer, new CancelAppointmentCommand(id), cancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var message = (await MessagesAsync(booking, cancellationToken)).Single(m => m.Type == "appointment.cancelled");
        using var data = JsonDocument.Parse(message.Payload);
        data.RootElement.GetProperty("cancelledByCustomer").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Rescheduling_should_leave_a_message_with_both_times()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var id = await mysql.BookAsync(booking, BookingData.At(10), cancellationToken);

        await mysql.SendAsStaffAsync(
            booking.Company, booking.Staff, new RescheduleAppointmentCommand(id, BookingData.At(14)), cancellationToken);

        var message = (await MessagesAsync(booking, cancellationToken)).Single(m => m.Type == "appointment.rescheduled");
        using var data = JsonDocument.Parse(message.Payload);
        var root = data.RootElement;
        root.GetProperty("previousStartUtc").GetDateTime().ToUniversalTime().ShouldBe(BookingData.At(10));
        root.GetProperty("newStartUtc").GetDateTime().ToUniversalTime().ShouldBe(BookingData.At(14));
        root.GetProperty("rescheduledBy").GetGuid().ShouldBe(booking.Staff);
        root.GetProperty("rescheduledByCustomer").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task A_refused_change_should_leave_no_message()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var id = await mysql.BookAsync(booking, BookingData.At(10), cancellationToken);
        await mysql.BookAsync(booking, BookingData.At(14), cancellationToken);

        // A conflict is a failed Result: nothing is saved, so no message either.
        var conflict = await mysql.SendAsStaffAsync(
            booking.Company, booking.Staff, new RescheduleAppointmentCommand(id, BookingData.At(14, 15)), cancellationToken);

        conflict.IsSuccess.ShouldBeFalse();
        (await MessagesAsync(booking, cancellationToken)).Select(message => message.Type)
            .ShouldAllBe(type => type == "appointment.created");
    }

    [Fact]
    public async Task A_rule_of_the_Domain_that_refuses_should_leave_no_message()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var tattoo = await mysql.CreateOnRequestServiceAsync(booking, cancellationToken);
        var id = await mysql.BookAsync(booking, BookingData.At(10), cancellationToken, tattoo);

        await Should.ThrowAsync<DomainException>(() => mysql.SendAsStaffAsync(
            booking.Company, booking.Staff, new ConfirmAppointmentCommand(id), cancellationToken));

        (await MessagesAsync(booking, cancellationToken)).Select(message => message.Type).ToList()
            .ShouldBe(["appointment.created"], ignoreOrder: false);
    }

    [Fact]
    public async Task Setting_a_price_should_not_leave_a_message()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var tattoo = await mysql.CreateOnRequestServiceAsync(booking, cancellationToken);
        var id = await mysql.BookAsync(booking, BookingData.At(10), cancellationToken, tattoo);
        var item = (await mysql.LoadAppointmentAsync(booking.Company, id, cancellationToken))!.Items.Single().Id;

        var result = await mysql.SendAsStaffAsync(
            booking.Company, booking.Staff, new SetAppointmentItemPriceCommand(id, item, 300m), cancellationToken);

        // The Domain raises no event for it yet (open decision: notify the customer when the price is set).
        result.IsSuccess.ShouldBeTrue();
        (await MessagesAsync(booking, cancellationToken)).Select(message => message.Type).ToList()
            .ShouldBe(["appointment.created"], ignoreOrder: false);
    }

    /// <summary>The table is shared, so the test only looks at the company it created, in the order they happened.</summary>
    private async Task<List<OutboxMessage>> MessagesAsync(Booking booking, CancellationToken cancellationToken) =>
        (await OutboxData.AllAsync(mysql, cancellationToken))
            .Where(message => message.CompanyId == booking.Company)
            .ToList();
}