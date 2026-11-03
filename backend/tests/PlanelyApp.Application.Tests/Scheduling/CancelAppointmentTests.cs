using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Scheduling.Appointments.Cancel;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.SeedWork;
using Shouldly;
using CompanyData = PlanelyApp.Application.Tests.Organization.Companies.CompanyData;

namespace PlanelyApp.Application.Tests.Scheduling;

public class CancelAppointmentTests
{
    private readonly AppointmentScenario _scenario = new();

    [Fact]
    public async Task The_staff_should_cancel_and_the_reason_should_be_kept()
    {
        var appointment = _scenario.Confirmed();

        var result = await Handler(appointment).HandleAsync(
            new CancelAppointmentCommand(appointment.Id, "Professional is ill"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        appointment.Status.ShouldBe(AppointmentStatus.Cancelled);
        var change = appointment.History.Last();
        change.ChangedBy.ShouldBe(_scenario.Staff.Id);
        change.Reason.ShouldBe("Professional is ill");
    }

    [Fact]
    public async Task The_staff_should_cancel_even_after_the_deadline_of_the_customer()
    {
        var appointment = _scenario.Confirmed();

        // One hour before the start; the deadline of the policy is 24 hours.
        var result = await Handler(appointment, AvailabilityData.At(9)).HandleAsync(
            new CancelAppointmentCommand(appointment.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        appointment.Status.ShouldBe(AppointmentStatus.Cancelled);
    }

    [Fact]
    public async Task A_customer_should_cancel_their_own_appointment_before_the_deadline()
    {
        _scenario.ActAsCustomer();
        var appointment = _scenario.Confirmed();

        var result = await Handler(appointment).HandleAsync(
            new CancelAppointmentCommand(appointment.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        appointment.Status.ShouldBe(AppointmentStatus.Cancelled);
        appointment.History.Last().ChangedBy.ShouldBe(_scenario.CustomerId);
    }

    [Fact]
    public async Task A_customer_after_the_deadline_should_be_refused_by_the_Domain()
    {
        _scenario.ActAsCustomer();
        var appointment = _scenario.Confirmed();

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(appointment, AvailabilityData.At(9)).HandleAsync(
            new CancelAppointmentCommand(appointment.Id), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Appointment.CancellationDeadlinePassed");
        appointment.Status.ShouldBe(AppointmentStatus.Confirmed);
    }

    [Fact]
    public async Task A_customer_should_not_cancel_the_appointment_of_another()
    {
        _scenario.ActAsCustomer(Guid.NewGuid());
        var appointment = _scenario.Confirmed();

        var result = await Handler(appointment).HandleAsync(
            new CancelAppointmentCommand(appointment.Id), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Appointment.NotFound");
        appointment.Status.ShouldBe(AppointmentStatus.Confirmed);
    }

    [Fact]
    public async Task A_company_that_does_not_exist_should_be_not_found()
    {
        var appointment = _scenario.Confirmed();

        var result = await Handler(appointment, companies: CompanyData.RepositoryWith(null)).HandleAsync(
            new CancelAppointmentCommand(appointment.Id), TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Company.NotFound");
    }

    [Fact]
    public async Task An_appointment_already_cancelled_should_be_refused_by_the_Domain()
    {
        var appointment = _scenario.Confirmed();
        appointment.CancelByStaff(_scenario.Staff.Id, AvailabilityData.Now);

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(appointment).HandleAsync(
            new CancelAppointmentCommand(appointment.Id), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Appointment.InvalidStatusTransition");
    }

    [Fact]
    public async Task An_appointment_that_does_not_exist_should_be_not_found()
    {
        var result = await Handler(null).HandleAsync(
            new CancelAppointmentCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Appointment.NotFound");
    }

    [Fact]
    public async Task An_empty_id_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new CancelAppointmentValidator(), new CancelAppointmentCommand(Guid.Empty));

        problems.ShouldBe(["AppointmentId:Validation.Required"], ignoreOrder: true);
    }

    private CancelAppointmentHandler Handler(
        Appointment? appointment,
        DateTime? now = null,
        ICompanyRepository? companies = null) =>
        new(
            _scenario.Access(appointment),
            companies ?? CompanyData.RepositoryWith(_scenario.Company),
            _scenario.Actor,
            _scenario.Tenant,
            new FixedTimeProvider(now ?? AvailabilityData.Now));
}