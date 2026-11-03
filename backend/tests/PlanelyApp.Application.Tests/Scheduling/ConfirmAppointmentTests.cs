using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Scheduling.Appointments.Confirm;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests.Scheduling;

public class ConfirmAppointmentTests
{
    private readonly AppointmentScenario _scenario = new();

    [Fact]
    public async Task A_pending_appointment_should_be_confirmed_by_the_staff()
    {
        var appointment = _scenario.Pending();

        var result = await Handler(appointment).HandleAsync(
            new ConfirmAppointmentCommand(appointment.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        appointment.Status.ShouldBe(AppointmentStatus.Confirmed);
        appointment.History.Count.ShouldBe(2);
        appointment.History.Last().ChangedBy.ShouldBe(_scenario.Staff.Id);
    }

    [Fact]
    public async Task A_service_on_request_without_a_price_should_be_refused_by_the_Domain()
    {
        var appointment = _scenario.PendingWithoutPrice();

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(appointment).HandleAsync(
            new ConfirmAppointmentCommand(appointment.Id), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Appointment.PriceStillPending");
        appointment.Status.ShouldBe(AppointmentStatus.Pending);
    }

    [Fact]
    public async Task An_appointment_that_is_already_confirmed_should_be_refused_by_the_Domain()
    {
        var appointment = _scenario.Confirmed();

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(appointment).HandleAsync(
            new ConfirmAppointmentCommand(appointment.Id), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Appointment.InvalidStatusTransition");
    }

    [Fact]
    public async Task An_appointment_that_does_not_exist_should_be_not_found()
    {
        var result = await Handler(null).HandleAsync(
            new ConfirmAppointmentCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Appointment.NotFound");
    }

    [Fact]
    public async Task A_customer_should_not_confirm()
    {
        _scenario.ActAsCustomer();
        var appointment = _scenario.Pending();

        var result = await Handler(appointment).HandleAsync(
            new ConfirmAppointmentCommand(appointment.Id), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Appointment.StaffOnly");
        appointment.Status.ShouldBe(AppointmentStatus.Pending);
    }

    [Fact]
    public async Task A_valid_command_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new ConfirmAppointmentValidator(), new ConfirmAppointmentCommand(Guid.NewGuid()));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_empty_id_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new ConfirmAppointmentValidator(), new ConfirmAppointmentCommand(Guid.Empty));

        problems.ShouldBe(["AppointmentId:Validation.Required"], ignoreOrder: true);
    }

    private ConfirmAppointmentHandler Handler(Appointment? appointment) =>
        new(_scenario.Access(appointment), _scenario.Actor, new FixedTimeProvider(AvailabilityData.Now));
}