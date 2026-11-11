using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Scheduling.Appointments.SetItemPrice;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;
using CompanyData = PlanelyApp.Application.Tests.Organization.Companies.CompanyData;

namespace PlanelyApp.Application.Tests.Scheduling;

public class SetAppointmentItemPriceTests
{
    private readonly AppointmentScenario _scenario = new();

    // ---- The way it works ----

    [Fact]
    public async Task The_price_should_complete_the_total_and_leave_the_period_as_it_was()
    {
        var appointment = _scenario.PendingWithoutPrice();
        var before = _scenario.Professional.ScheduleVersion;

        var result = await Handler(appointment).HandleAsync(
            Price(appointment, 300m), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        appointment.Items.Single().FinalPrice.ShouldBe(Money.From(300m));
        appointment.TotalPrice.ShouldBe(Money.From(300m));
        appointment.HasPendingPrice.ShouldBeFalse();
        appointment.Period.End.ShouldBe(AvailabilityData.At(12));
        appointment.Status.ShouldBe(AppointmentStatus.Pending);
        _scenario.Professional.ScheduleVersion.ShouldBe(before + 1);
        await _scenario.Appointments.DidNotReceive().ListOccupyingScheduleAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task After_the_price_the_appointment_should_be_confirmable()
    {
        var appointment = _scenario.PendingWithoutPrice();

        await Handler(appointment).HandleAsync(Price(appointment, 300m), TestContext.Current.CancellationToken);
        appointment.Confirm(_scenario.Staff.Id, AvailabilityData.Now);

        appointment.Status.ShouldBe(AppointmentStatus.Confirmed);
    }

    [Fact]
    public async Task A_longer_duration_into_free_time_should_grow_the_period()
    {
        var appointment = _scenario.PendingWithoutPrice();

        var result = await Handler(appointment).HandleAsync(
            Price(appointment, 300m, 180), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        appointment.Items.Single().Duration.ShouldBe(TimeSpan.FromMinutes(180));
        appointment.Period.End.ShouldBe(AvailabilityData.At(13));
    }

    [Fact]
    public async Task A_longer_duration_into_another_appointment_should_be_a_conflict()
    {
        var appointment = _scenario.PendingWithoutPrice();
        _scenario.Booked(_scenario.Confirmed(AvailabilityData.At(12, 30)));
        var before = _scenario.Professional.ScheduleVersion;

        var result = await Handler(appointment).HandleAsync(
            Price(appointment, 300m, 180), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Appointment.TimeNotAvailable");
        _scenario.Professional.ScheduleVersion.ShouldBe(before);
    }

    [Fact]
    public async Task A_shorter_duration_should_free_time_without_looking_at_the_agenda()
    {
        var appointment = _scenario.PendingWithoutPrice();

        var result = await Handler(appointment).HandleAsync(
            Price(appointment, 300m, 60), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        appointment.Period.End.ShouldBe(AvailabilityData.At(11));
        await _scenario.Appointments.DidNotReceive().ListOccupyingScheduleAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_price_without_more_room_should_not_need_an_active_professional()
    {
        var appointment = _scenario.PendingWithoutPrice();
        _scenario.Professional.Deactivate();

        var result = await Handler(appointment).HandleAsync(
            Price(appointment, 300m), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task A_longer_duration_should_need_an_active_professional()
    {
        var appointment = _scenario.PendingWithoutPrice();
        _scenario.Professional.Deactivate();

        var result = await Handler(appointment).HandleAsync(
            Price(appointment, 300m, 180), TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Professional.Inactive");
    }

    // ---- The rules of the Domain (422) ----

    [Fact]
    public async Task An_appointment_that_is_not_pending_should_be_refused_by_the_Domain()
    {
        var appointment = _scenario.Confirmed();

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(appointment).HandleAsync(
            Price(appointment, 300m), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Appointment.PriceCanOnlyBeSetWhilePending");
    }

    [Fact]
    public async Task A_service_of_fixed_price_should_be_refused_by_the_Domain()
    {
        var appointment = _scenario.Pending();

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(appointment).HandleAsync(
            Price(appointment, 300m), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Appointment.ItemHasFixedPrice");
    }

    [Fact]
    public async Task An_item_that_is_not_in_the_appointment_should_be_refused_by_the_Domain()
    {
        var appointment = _scenario.PendingWithoutPrice();

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(appointment).HandleAsync(
            new SetAppointmentItemPriceCommand(appointment.Id, Guid.NewGuid(), 300m),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Appointment.ItemNotFound");
    }

    [Fact]
    public async Task A_negative_price_should_be_refused_by_the_Domain()
    {
        var appointment = _scenario.PendingWithoutPrice();

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(appointment).HandleAsync(
            Price(appointment, -1m), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Money.NegativeAmount");
        appointment.HasPendingPrice.ShouldBeTrue();
    }

    [Fact]
    public async Task A_duration_of_zero_should_be_refused_by_the_Domain()
    {
        var appointment = _scenario.PendingWithoutPrice();

        var exception = await Should.ThrowAsync<DomainException>(() => Handler(appointment).HandleAsync(
            Price(appointment, 300m, 0), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("Appointment.ItemDurationInvalid");
    }

    // ---- Who is acting, and what exists ----

    [Fact]
    public async Task A_customer_should_not_set_a_price()
    {
        _scenario.ActAsCustomer();
        var appointment = _scenario.PendingWithoutPrice();

        var result = await Handler(appointment).HandleAsync(
            Price(appointment, 300m), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Appointment.StaffOnly");
        appointment.HasPendingPrice.ShouldBeTrue();
    }

    [Fact]
    public async Task An_appointment_that_does_not_exist_should_be_not_found()
    {
        var result = await Handler(null).HandleAsync(
            new SetAppointmentItemPriceCommand(Guid.NewGuid(), Guid.NewGuid(), 300m),
            TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Appointment.NotFound");
    }

    [Fact]
    public async Task A_company_that_does_not_exist_should_be_not_found()
    {
        _scenario.Companies = CompanyData.RepositoryWith(null);
        var appointment = _scenario.PendingWithoutPrice();

        var result = await Handler(appointment).HandleAsync(
            Price(appointment, 300m), TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Company.NotFound");
    }

    // ---- The validator ----

    [Fact]
    public async Task A_valid_command_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new SetAppointmentItemPriceValidator(),
            new SetAppointmentItemPriceCommand(Guid.NewGuid(), Guid.NewGuid(), 300m, 90));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task Empty_ids_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new SetAppointmentItemPriceValidator(),
            new SetAppointmentItemPriceCommand(Guid.Empty, Guid.Empty, 300m));

        problems.ShouldBe(["AppointmentId:Validation.Required", "ItemId:Validation.Required"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_price_with_more_than_two_decimals_should_be_reported_as_invalid()
    {
        var problems = await TestValidation.RunAsync(
            new SetAppointmentItemPriceValidator(),
            new SetAppointmentItemPriceCommand(Guid.NewGuid(), Guid.NewGuid(), 10.123m));

        problems.ShouldBe(["Price:Validation.Invalid"], ignoreOrder: true);
    }

    private static SetAppointmentItemPriceCommand Price(Appointment appointment, decimal price, int? minutes = null) =>
        new(appointment.Id, appointment.Items[0].Id, price, minutes);

    private SetAppointmentItemPriceHandler Handler(Appointment? appointment) =>
        new(_scenario.Access(appointment), _scenario.Agenda(), _scenario.Companies, _scenario.Tenant);
}