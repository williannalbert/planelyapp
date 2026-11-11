using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Scheduling.Appointments.Start;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests.Scheduling;

public class StartAppointmentTests
{
    private readonly AppointmentScenario _scenario = new();

    [Fact]
    public async Task A_confirmed_appointment_should_start()
    {
        var appointment = _scenario.Confirmed();

        var result = await Handler(appointment).HandleAsync(
            new StartAppointmentCommand(appointment.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        appointment.Status.ShouldBe(AppointmentStatus.InProgress);
        appointment.History.Last().ChangedBy.ShouldBe(_scenario.Staff.Id);
    }

    [Fact]
    public async Task A_pending_appointment_should_be_refused_by_the_Domain()
    {
        var appointment = _scenario.Pending();

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(appointment).HandleAsync(
            new StartAppointmentCommand(appointment.Id), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Appointment.InvalidStatusTransition");
    }

    [Fact]
    public async Task An_appointment_that_does_not_exist_should_be_not_found()
    {
        var result = await Handler(null).HandleAsync(
            new StartAppointmentCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Appointment.NotFound");
    }

    [Fact]
    public async Task A_customer_should_not_start()
    {
        _scenario.ActAsCustomer();
        var appointment = _scenario.Confirmed();

        var result = await Handler(appointment).HandleAsync(
            new StartAppointmentCommand(appointment.Id), TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Appointment.StaffOnly");
    }

    [Fact]
    public async Task An_empty_id_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new StartAppointmentValidator(), new StartAppointmentCommand(Guid.Empty));

        problems.ShouldBe(["AppointmentId:Validation.Required"], ignoreOrder: true);
    }

    private StartAppointmentHandler Handler(Appointment? appointment) =>
        new(_scenario.Access(appointment), _scenario.Actor, new FixedTimeProvider(AvailabilityData.At(10)));
}