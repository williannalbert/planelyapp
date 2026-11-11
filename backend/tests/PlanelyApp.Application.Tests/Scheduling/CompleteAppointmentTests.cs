using PlanelyApp.Application.Scheduling.Appointments.Complete;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests.Scheduling;

public class CompleteAppointmentTests
{
    private readonly AppointmentScenario _scenario = new();

    [Fact]
    public async Task An_appointment_in_progress_should_be_completed()
    {
        var appointment = _scenario.InProgress();

        var result = await Handler(appointment).HandleAsync(
            new CompleteAppointmentCommand(appointment.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        appointment.Status.ShouldBe(AppointmentStatus.Completed);
    }

    [Fact]
    public async Task An_appointment_that_has_not_started_should_be_refused_by_the_Domain()
    {
        var appointment = _scenario.Confirmed();

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(appointment).HandleAsync(
            new CompleteAppointmentCommand(appointment.Id), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Appointment.InvalidStatusTransition");
    }

    [Fact]
    public async Task An_appointment_that_does_not_exist_should_be_not_found()
    {
        var result = await Handler(null).HandleAsync(
            new CompleteAppointmentCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Appointment.NotFound");
    }

    [Fact]
    public async Task An_empty_id_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new CompleteAppointmentValidator(), new CompleteAppointmentCommand(Guid.Empty));

        problems.ShouldBe(["AppointmentId:Validation.Required"], ignoreOrder: true);
    }

    private CompleteAppointmentHandler Handler(Appointment? appointment) =>
        new(_scenario.Access(appointment), _scenario.Actor, new FixedTimeProvider(AvailabilityData.At(11)));
}