using NSubstitute;
using PlanelyApp.Application.Abstractions.Events;
using PlanelyApp.Application.Abstractions.Messaging;
using PlanelyApp.Application.Scheduling.Appointments.Events;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Application.Tests.Scheduling;

public class AppointmentOutboxHandlersTests
{
    private static readonly Guid AppointmentId = Guid.CreateVersion7();
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly Guid BranchId = Guid.CreateVersion7();
    private static readonly Guid CustomerId = Guid.CreateVersion7();
    private static readonly Guid ProfessionalId = Guid.CreateVersion7();
    private static readonly Guid ActorId = Guid.CreateVersion7();
    private static readonly DateTime Start = new(2027, 11, 22, 13, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = Start.AddMinutes(90);
    private static readonly DateTime Occurred = new(2027, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(AppointmentStatus.Pending)]
    [InlineData(AppointmentStatus.Confirmed)]
    public async Task A_created_appointment_should_leave_a_message_with_its_status(AppointmentStatus status)
    {
        var outbox = Substitute.For<IOutbox>();
        var domainEvent = new AppointmentCreated(
            AppointmentId, CompanyId, BranchId, CustomerId, ProfessionalId, Start, status)
        { OccurredAtUtc = Occurred };

        await new AppointmentCreatedOutboxHandler(outbox).HandleAsync(domainEvent, TestContext.Current.CancellationToken);

        var expected = new AppointmentCreatedMessage(AppointmentId, BranchId, CustomerId, ProfessionalId, Start, status);
        outbox.Received(1).Add("appointment.created", CompanyId, Arg.Is<object>(data => data.Equals(expected)), Occurred, 1);
    }

    [Fact]
    public async Task A_confirmed_appointment_should_leave_a_message_with_the_total()
    {
        var outbox = Substitute.For<IOutbox>();
        var domainEvent = new AppointmentConfirmed(
            AppointmentId, CompanyId, BranchId, CustomerId, ProfessionalId, Start, Money.From(850m))
        { OccurredAtUtc = Occurred };

        await new AppointmentConfirmedOutboxHandler(outbox).HandleAsync(domainEvent, TestContext.Current.CancellationToken);

        var expected = new AppointmentConfirmedMessage(AppointmentId, BranchId, CustomerId, ProfessionalId, Start, 850m);
        outbox.Received(1).Add("appointment.confirmed", CompanyId, Arg.Is<object>(data => data.Equals(expected)), Occurred, 1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_cancelled_appointment_should_say_who_cancelled_and_never_send_the_reason(bool byCustomer)
    {
        var outbox = Substitute.For<IOutbox>();
        var domainEvent = new AppointmentCancelled(
            AppointmentId, CompanyId, BranchId, CustomerId, ProfessionalId, Start,
            "Passei mal, me liguem no (11) 99999-0000", byCustomer)
        { OccurredAtUtc = Occurred };

        await new AppointmentCancelledOutboxHandler(outbox).HandleAsync(domainEvent, TestContext.Current.CancellationToken);

        // The message has no property for the reason, so equality with this record proves it is not sent.
        var expected = new AppointmentCancelledMessage(AppointmentId, BranchId, CustomerId, ProfessionalId, Start, byCustomer);
        outbox.Received(1).Add("appointment.cancelled", CompanyId, Arg.Is<object>(data => data.Equals(expected)), Occurred, 1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_rescheduled_appointment_should_carry_both_times_and_who_moved_it(bool byCustomer)
    {
        var outbox = Substitute.For<IOutbox>();
        var domainEvent = new AppointmentRescheduled(
            AppointmentId, CompanyId, BranchId, CustomerId, ProfessionalId, Start, Start.AddHours(3), ActorId, byCustomer)
        { OccurredAtUtc = Occurred };

        await new AppointmentRescheduledOutboxHandler(outbox).HandleAsync(domainEvent, TestContext.Current.CancellationToken);

        var expected = new AppointmentRescheduledMessage(
            AppointmentId, BranchId, CustomerId, ProfessionalId, Start, Start.AddHours(3), ActorId, byCustomer);
        outbox.Received(1).Add("appointment.rescheduled", CompanyId, Arg.Is<object>(data => data.Equals(expected)), Occurred, 1);
    }

    [Theory]
    [InlineData(300)]
    [InlineData(null)]
    public async Task A_change_of_services_should_carry_the_period_and_the_total_if_it_is_known(int? total)
    {
        var outbox = Substitute.For<IOutbox>();
        var price = total is { } value ? Money.From(value) : null;
        var domainEvent = new AppointmentServicesChanged(
            AppointmentId, CompanyId, BranchId, CustomerId, ProfessionalId, Start, End, price, ActorId, false)
        { OccurredAtUtc = Occurred };

        await new AppointmentServicesChangedOutboxHandler(outbox).HandleAsync(domainEvent, TestContext.Current.CancellationToken);

        var expected = new AppointmentServicesChangedMessage(
            AppointmentId, BranchId, CustomerId, ProfessionalId, Start, End, total, ActorId, false);
        outbox.Received(1).Add("appointment.services_changed", CompanyId, Arg.Is<object>(data => data.Equals(expected)), Occurred, 1);
    }

    [Fact]
    public void Every_domain_event_of_the_appointment_should_have_an_outbox_handler()
    {
        var events = typeof(AppointmentCreated).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(AppointmentCreated).Namespace
                && !type.IsAbstract
                && typeof(IDomainEvent).IsAssignableFrom(type));

        var handled = typeof(AssemblyReference).Assembly.GetTypes()
            .SelectMany(type => type.GetInterfaces())
            .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))
            .Select(contract => contract.GetGenericArguments()[0])
            .ToList();

        // A new event in the Domain without a message is a decision someone has to make on purpose.
        events.Except(handled).ShouldBeEmpty();
    }
}