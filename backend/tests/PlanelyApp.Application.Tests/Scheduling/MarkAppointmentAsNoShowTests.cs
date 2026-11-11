using PlanelyApp.Application.Scheduling.Appointments.MarkNoShow;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests.Scheduling;

public class MarkAppointmentAsNoShowTests
{
    private readonly AppointmentScenario _scenario = new();

    [Fact]
    public async Task After_the_start_time_a_confirmed_appointment_should_be_marked_as_no_show()
    {
        var appointment = _scenario.Confirmed();

        var result = await Handler(appointment, AvailabilityData.At(11)).HandleAsync(
            new MarkAppointmentAsNoShowCommand(appointment.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        appointment.Status.ShouldBe(AppointmentStatus.NoShow);
        appointment.History.Last().ChangedBy.ShouldBe(_scenario.Staff.Id);
    }

    [Fact]
    public async Task Before_the_start_time_it_should_be_refused_by_the_Domain()
    {
        var appointment = _scenario.Confirmed();

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(appointment, AvailabilityData.At(9)).HandleAsync(
            new MarkAppointmentAsNoShowCommand(appointment.Id), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Appointment.NoShowBeforeStart");
        appointment.Status.ShouldBe(AppointmentStatus.Confirmed);
    }

    [Fact]
    public async Task An_appointment_that_does_not_exist_should_be_not_found()
    {
        var result = await Handler(null, AvailabilityData.At(11)).HandleAsync(
            new MarkAppointmentAsNoShowCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Appointment.NotFound");
    }

    [Fact]
    public async Task An_empty_id_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new MarkAppointmentAsNoShowValidator(), new MarkAppointmentAsNoShowCommand(Guid.Empty));

        problems.ShouldBe(["AppointmentId:Validation.Required"], ignoreOrder: true);
    }

    private MarkAppointmentAsNoShowHandler Handler(Appointment? appointment, DateTime now) =>
        new(_scenario.Access(appointment), _scenario.Actor, new FixedTimeProvider(now));
}