using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Scheduling.Appointments.Remind;
using PlanelyApp.Domain.Scheduling.Appointments;
using Shouldly;

namespace PlanelyApp.Application.Tests.Scheduling;

public class SendAppointmentReminderTests
{
    private readonly AppointmentScenario _scenario = new();

    [Fact]
    public async Task A_confirmed_appointment_should_be_reminded()
    {
        var appointment = _scenario.Confirmed();

        var result = await Handler(appointment).HandleAsync(
            new SendAppointmentReminderCommand(appointment.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
        appointment.ReminderSentAtUtc.ShouldBe(AvailabilityData.Now);
        var domainEvent = appointment.DomainEvents.OfType<AppointmentReminderDue>().Single();
        domainEvent.AppointmentId.ShouldBe(appointment.Id);
        domainEvent.CompanyId.ShouldBe(_scenario.Company.Id);
        domainEvent.StartUtc.ShouldBe(appointment.Period.Start);
    }

    [Fact]
    public async Task An_appointment_already_reminded_should_be_left_alone()
    {
        var appointment = _scenario.Confirmed();
        var handler = Handler(appointment);
        var command = new SendAppointmentReminderCommand(appointment.Id);

        await handler.HandleAsync(command, TestContext.Current.CancellationToken);
        var second = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        second.IsSuccess.ShouldBeTrue();
        second.Value.ShouldBeFalse();
        appointment.DomainEvents.OfType<AppointmentReminderDue>().Count().ShouldBe(1);
    }

    [Fact]
    public async Task A_pending_appointment_should_not_be_reminded()
    {
        var appointment = _scenario.Pending();

        var result = await Handler(appointment).HandleAsync(
            new SendAppointmentReminderCommand(appointment.Id), TestContext.Current.CancellationToken);

        result.Value.ShouldBeFalse();
        appointment.ReminderSentAtUtc.ShouldBeNull();
        appointment.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_cancelled_appointment_should_not_be_reminded()
    {
        var appointment = _scenario.Confirmed();
        appointment.CancelByStaff(_scenario.Staff.Id, AvailabilityData.Now);

        var result = await Handler(appointment).HandleAsync(
            new SendAppointmentReminderCommand(appointment.Id), TestContext.Current.CancellationToken);

        result.Value.ShouldBeFalse();
        appointment.ReminderSentAtUtc.ShouldBeNull();
    }

    [Fact]
    public async Task An_appointment_that_has_started_should_not_be_reminded()
    {
        var appointment = _scenario.Confirmed();

        var result = await Handler(appointment, appointment.Period.Start).HandleAsync(
            new SendAppointmentReminderCommand(appointment.Id), TestContext.Current.CancellationToken);

        result.Value.ShouldBeFalse();
        appointment.ReminderSentAtUtc.ShouldBeNull();
    }

    [Fact]
    public async Task An_appointment_that_does_not_exist_should_be_not_found()
    {
        var result = await Handler(null).HandleAsync(
            new SendAppointmentReminderCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Appointment.NotFound");
    }

    [Fact]
    public async Task A_valid_command_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new SendAppointmentReminderValidator(), new SendAppointmentReminderCommand(Guid.NewGuid()));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_empty_id_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new SendAppointmentReminderValidator(), new SendAppointmentReminderCommand(Guid.Empty));

        problems.ShouldBe(["AppointmentId:Validation.Required"], ignoreOrder: true);
    }

    private SendAppointmentReminderHandler Handler(Appointment? appointment, DateTime? nowUtc = null)
    {
        _scenario.Appointments.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(appointment);

        return new SendAppointmentReminderHandler(
            _scenario.Appointments, new FixedTimeProvider(nowUtc ?? AvailabilityData.Now));
    }
}