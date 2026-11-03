using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Scheduling.Appointments.Reschedule;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;
using ProfessionalData = PlanelyApp.Application.Tests.Organization.Professionals.ProfessionalData;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;
using CompanyData = PlanelyApp.Application.Tests.Organization.Companies.CompanyData;

namespace PlanelyApp.Application.Tests.Scheduling;

public class RescheduleAppointmentTests
{
    private readonly AppointmentScenario _scenario = new();

    // ---- The way it works ----

    [Fact]
    public async Task The_staff_should_move_a_confirmed_appointment_to_a_free_time()
    {
        var appointment = _scenario.Confirmed();
        var before = _scenario.Professional.ScheduleVersion;

        var result = await Handler(appointment).HandleAsync(
            Move(appointment, AvailabilityData.At(14)), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        appointment.Period.ShouldBe(DateTimeRange.Create(AvailabilityData.At(14), AvailabilityData.At(14, 30)));
        appointment.Status.ShouldBe(AppointmentStatus.Confirmed);
        _scenario.Professional.ScheduleVersion.ShouldBe(before + 1);
    }

    [Fact]
    public async Task A_customer_should_move_their_own_appointment()
    {
        _scenario.ActAsCustomer();
        var appointment = _scenario.Confirmed();
        var before = _scenario.Professional.ScheduleVersion;

        var result = await Handler(appointment).HandleAsync(
            Move(appointment, AvailabilityData.At(14)), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        appointment.Period.Start.ShouldBe(AvailabilityData.At(14));
        _scenario.Professional.ScheduleVersion.ShouldBe(before + 1);
    }

    [Fact]
    public async Task The_new_time_may_overlap_the_old_time_of_the_same_appointment()
    {
        var appointment = _scenario.Confirmed();
        _scenario.Booked(appointment);

        var result = await Handler(appointment).HandleAsync(
            Move(appointment, AvailabilityData.At(10, 15)), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        appointment.Period.Start.ShouldBe(AvailabilityData.At(10, 15));
    }

    [Fact]
    public async Task Another_appointment_of_the_professional_in_the_way_should_be_a_conflict()
    {
        var appointment = _scenario.Confirmed();
        _scenario.Booked(_scenario.Confirmed(AvailabilityData.At(14)));
        var before = _scenario.Professional.ScheduleVersion;

        var result = await Handler(appointment).HandleAsync(
            Move(appointment, AvailabilityData.At(14, 15)), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Appointment.TimeNotAvailable");
        _scenario.Professional.ScheduleVersion.ShouldBe(before);
    }

    [Fact]
    public async Task A_time_outside_the_working_hours_should_be_a_conflict()
    {
        var appointment = _scenario.Confirmed();
        var before = _scenario.Professional.ScheduleVersion;

        // The branch closes at 18:00.
        var result = await Handler(appointment).HandleAsync(
            Move(appointment, AvailabilityData.At(18)), TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Appointment.TimeNotAvailable");
        _scenario.Professional.ScheduleVersion.ShouldBe(before);
    }

    [Fact]
    public async Task Moving_to_the_same_start_should_change_nothing()
    {
        var appointment = _scenario.Confirmed();
        var before = _scenario.Professional.ScheduleVersion;

        var result = await Handler(appointment).HandleAsync(
            Move(appointment, AvailabilityData.At(10)), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        _scenario.Professional.ScheduleVersion.ShouldBe(before);
        appointment.DomainEvents.ShouldBeEmpty();
        await _scenario.Appointments.DidNotReceive().ListOccupyingScheduleAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }

    // ---- The rules of the Domain (422) ----

    [Fact]
    public async Task A_customer_after_the_deadline_should_be_refused_by_the_Domain()
    {
        _scenario.ActAsCustomer();
        var appointment = _scenario.Confirmed();

        // One hour before the start; the deadline of the policy is 24 hours.
        var exception = await Should.ThrowAsync<DomainException>(() =>
            Handler(appointment, AvailabilityData.At(9)).HandleAsync(
                Move(appointment, AvailabilityData.At(14)), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Appointment.CancellationDeadlinePassed");
        appointment.Period.Start.ShouldBe(AvailabilityData.At(10));
    }

    [Fact]
    public async Task The_staff_after_the_deadline_should_move_it_anyway()
    {
        var appointment = _scenario.Confirmed();

        var result = await Handler(appointment, AvailabilityData.At(9)).HandleAsync(
            Move(appointment, AvailabilityData.At(14)), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        appointment.Period.Start.ShouldBe(AvailabilityData.At(14));
    }

    [Fact]
    public async Task A_new_start_without_the_minimum_notice_should_be_refused_by_the_Domain()
    {
        var appointment = _scenario.Confirmed();

        // It is 08:00 and the policy asks for two hours.
        var exception = await Should.ThrowAsync<DomainException>(() =>
            Handler(appointment, AvailabilityData.At(8)).HandleAsync(
                Move(appointment, AvailabilityData.At(9)), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Appointment.MinimumNoticeNotMet");
    }

    [Fact]
    public async Task A_new_start_beyond_the_window_should_be_refused_by_the_Domain()
    {
        var appointment = _scenario.Confirmed();

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(appointment).HandleAsync(
            Move(appointment, AvailabilityData.At(10).AddDays(70)), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Appointment.BeyondBookingWindow");
    }

    [Fact]
    public async Task A_cancelled_appointment_should_not_be_moved()
    {
        var appointment = _scenario.Confirmed();
        appointment.CancelByStaff(_scenario.Staff.Id, AvailabilityData.Now);

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(appointment).HandleAsync(
            Move(appointment, AvailabilityData.At(14)), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Appointment.NotEditable");
    }

    [Fact]
    public async Task An_appointment_in_progress_should_not_be_moved()
    {
        var appointment = _scenario.InProgress();

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(appointment).HandleAsync(
            Move(appointment, AvailabilityData.At(14)), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Appointment.NotEditable");
    }

    // ---- Who is acting, and what exists ----

    [Fact]
    public async Task The_appointment_of_another_customer_should_look_like_it_does_not_exist()
    {
        _scenario.ActAsCustomer(Guid.NewGuid());
        var appointment = _scenario.Confirmed();

        var result = await Handler(appointment).HandleAsync(
            Move(appointment, AvailabilityData.At(14)), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Appointment.NotFound");
        appointment.Period.Start.ShouldBe(AvailabilityData.At(10));
    }

    [Fact]
    public async Task An_appointment_that_does_not_exist_should_be_not_found()
    {
        var result = await Handler(null).HandleAsync(
            new RescheduleAppointmentCommand(Guid.NewGuid(), AvailabilityData.At(14)),
            TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Appointment.NotFound");
    }

    [Fact]
    public async Task Staff_without_access_to_the_branch_should_be_forbidden()
    {
        _scenario.Staff.SetBranchAccess(false, [Guid.NewGuid()]);
        var appointment = _scenario.Confirmed();

        var result = await Handler(appointment).HandleAsync(
            Move(appointment, AvailabilityData.At(14)), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Branch.AccessDenied");
    }

    [Fact]
    public async Task A_company_that_does_not_exist_should_be_not_found()
    {
        _scenario.Companies = CompanyData.RepositoryWith(null);
        var appointment = _scenario.Confirmed();

        var result = await Handler(appointment).HandleAsync(
            Move(appointment, AvailabilityData.At(14)), TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Company.NotFound");
    }

    [Fact]
    public async Task An_inactive_company_should_not_move_appointments()
    {
        _scenario.Company.Deactivate();
        var appointment = _scenario.Confirmed();

        var result = await Handler(appointment).HandleAsync(
            Move(appointment, AvailabilityData.At(14)), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Company.Inactive");
    }

    [Fact]
    public async Task An_inactive_professional_should_be_a_conflict()
    {
        var appointment = _scenario.Confirmed();
        _scenario.Professional.Deactivate();

        var result = await Handler(appointment).HandleAsync(
            Move(appointment, AvailabilityData.At(14)), TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Professional.Inactive");
    }

    [Fact]
    public async Task An_inactive_branch_should_be_a_conflict()
    {
        var appointment = _scenario.Confirmed();
        _scenario.Branch.Deactivate();

        var result = await Handler(appointment).HandleAsync(
            Move(appointment, AvailabilityData.At(14)), TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Branch.Inactive");
    }

    [Fact]
    public async Task A_professional_that_does_not_exist_should_be_not_found()
    {
        _scenario.Professionals = ProfessionalData.RepositoryWith(null);
        var appointment = _scenario.Confirmed();

        var result = await Handler(appointment).HandleAsync(
            Move(appointment, AvailabilityData.At(14)), TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Professional.NotFound");
    }

    [Fact]
    public async Task A_branch_that_does_not_exist_should_be_not_found()
    {
        _scenario.Branches = BranchData.RepositoryWith(null);
        var appointment = _scenario.Confirmed();

        var result = await Handler(appointment).HandleAsync(
            Move(appointment, AvailabilityData.At(14)), TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Branch.NotFound");
    }

    // ---- The validator ----

    [Fact]
    public async Task A_valid_command_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new RescheduleAppointmentValidator(),
            new RescheduleAppointmentCommand(Guid.NewGuid(), AvailabilityData.At(14)));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_empty_id_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new RescheduleAppointmentValidator(),
            new RescheduleAppointmentCommand(Guid.Empty, AvailabilityData.At(14)));

        problems.ShouldBe(["AppointmentId:Validation.Required"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_missing_start_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new RescheduleAppointmentValidator(),
            new RescheduleAppointmentCommand(Guid.NewGuid(), default));

        problems.ShouldBe(["NewStartUtc:Validation.Required"], ignoreOrder: true);
    }

    private static RescheduleAppointmentCommand Move(Appointment appointment, DateTime newStartUtc) =>
        new(appointment.Id, newStartUtc);

    private RescheduleAppointmentHandler Handler(Appointment? appointment, DateTime? now = null) =>
        new(
            _scenario.Access(appointment),
            _scenario.Agenda(),
            _scenario.Companies,
            _scenario.Actor,
            _scenario.Tenant,
            new FixedTimeProvider(now ?? AvailabilityData.Now));
}