using Microsoft.EntityFrameworkCore;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Scheduling.Appointments.Confirm;
using PlanelyApp.Application.Scheduling.Appointments.SetItemPrice;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Scheduling;

public class SetAppointmentItemPriceUseCaseTests(MySqlFixture mysql)
{
    [Fact]
    public async Task The_price_should_be_saved_and_then_the_appointment_should_be_confirmable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var tattoo = await mysql.CreateOnRequestServiceAsync(booking, cancellationToken);
        var id = await mysql.BookAsync(booking, BookingData.At(10), cancellationToken, tattoo);
        var item = (await mysql.LoadAppointmentAsync(booking.Company, id, cancellationToken))!.Items.Single().Id;
        var before = await mysql.LoadProfessionalAsync(booking.Company, booking.Ana, cancellationToken);

        var notYet = await Should.ThrowAsync<DomainException>(
            () => StaffAsync(booking, new ConfirmAppointmentCommand(id), cancellationToken));
        var priced = await StaffAsync(booking, new SetAppointmentItemPriceCommand(id, item, 300m), cancellationToken);
        var confirmed = await StaffAsync(booking, new ConfirmAppointmentCommand(id), cancellationToken);

        notYet.Code.ShouldBe("Appointment.PriceStillPending");
        priced.IsSuccess.ShouldBeTrue();
        confirmed.IsSuccess.ShouldBeTrue();
        var saved = await mysql.LoadAppointmentAsync(booking.Company, id, cancellationToken);
        saved.ShouldNotBeNull();
        saved.TotalPrice.ShouldBe(Money.From(300m));
        saved.Status.ShouldBe(AppointmentStatus.Confirmed);
        var after = await mysql.LoadProfessionalAsync(booking.Company, booking.Ana, cancellationToken);
        after!.ScheduleVersion.ShouldBe(before!.ScheduleVersion + 1);
    }

    [Fact]
    public async Task A_longer_duration_into_free_time_should_grow_the_period()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var tattoo = await mysql.CreateOnRequestServiceAsync(booking, cancellationToken);
        var id = await mysql.BookAsync(booking, BookingData.At(10), cancellationToken, tattoo);
        var item = (await mysql.LoadAppointmentAsync(booking.Company, id, cancellationToken))!.Items.Single().Id;

        var result = await StaffAsync(booking, new SetAppointmentItemPriceCommand(id, item, 300m, 180), cancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var saved = await mysql.LoadAppointmentAsync(booking.Company, id, cancellationToken);
        saved!.Items.Single().Duration.ShouldBe(TimeSpan.FromMinutes(180));
        // 10:00 plus 180 minutes plus the 10 minutes of interval of the company.
        saved.Period.End.ShouldBe(BookingData.At(13, 10));
    }

    [Fact]
    public async Task A_longer_duration_into_another_appointment_should_be_a_conflict_and_change_nothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var tattoo = await mysql.CreateOnRequestServiceAsync(booking, cancellationToken);
        var id = await mysql.BookAsync(booking, BookingData.At(10), cancellationToken, tattoo);
        await mysql.BookAsync(booking, BookingData.At(12, 30), cancellationToken);
        var item = (await mysql.LoadAppointmentAsync(booking.Company, id, cancellationToken))!.Items.Single().Id;

        var result = await StaffAsync(booking, new SetAppointmentItemPriceCommand(id, item, 300m, 180), cancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Appointment.TimeNotAvailable");
        var saved = await mysql.LoadAppointmentAsync(booking.Company, id, cancellationToken);
        saved!.TotalPrice.ShouldBeNull();
        saved.HasPendingPrice.ShouldBeTrue();
        saved.Items.Single().Duration.ShouldBe(TimeSpan.FromMinutes(120));
    }

    [Fact]
    public async Task Two_prices_set_at_the_same_moment_should_never_leave_a_priced_appointment_without_a_total()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var tattoo = await mysql.CreateOnRequestServiceAsync(booking, cancellationToken);
        var id = await mysql.BookAsync(booking, BookingData.At(10), cancellationToken, tattoo, tattoo);
        var items = (await mysql.LoadAppointmentAsync(booking.Company, id, cancellationToken))!.Items
            .Select(item => item.Id)
            .ToList();

        // Each request reads the appointment with both items still without a price. Without the schedule version, both
        // would be saved and the second total (still empty) would win over the first price.
        async Task TryPriceAsync(Guid item)
        {
            try
            {
                await StaffAsync(booking, new SetAppointmentItemPriceCommand(id, item, 100m), cancellationToken);
            }
            catch (DbUpdateException)
            {
                // The loser of the race: nothing of it was saved.
            }
        }

        await Task.WhenAll(TryPriceAsync(items[0]), TryPriceAsync(items[1]));

        // The total is empty exactly while some item has no price.
        var saved = await mysql.LoadAppointmentAsync(booking.Company, id, cancellationToken);
        saved.ShouldNotBeNull();
        (saved.TotalPrice is null).ShouldBe(saved.HasPendingPrice);
    }

    [Fact]
    public async Task A_customer_should_not_set_a_price()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var tattoo = await mysql.CreateOnRequestServiceAsync(booking, cancellationToken);
        var id = await mysql.BookAsync(booking, BookingData.At(10), cancellationToken, tattoo);
        var item = (await mysql.LoadAppointmentAsync(booking.Company, id, cancellationToken))!.Items.Single().Id;

        var result = await mysql.SendAsCustomerAsync(
            booking.Company,
            booking.Customer,
            new SetAppointmentItemPriceCommand(id, item, 300m),
            cancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Appointment.StaffOnly");
    }

    private Task<Result<Unit>> StaffAsync(Booking booking, ICommand<Unit> command, CancellationToken cancellationToken) =>
        mysql.SendAsStaffAsync(booking.Company, booking.Staff, command, cancellationToken);
}