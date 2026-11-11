using Microsoft.EntityFrameworkCore;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Scheduling.Appointments.Cancel;
using PlanelyApp.Application.Scheduling.Appointments.Reschedule;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Scheduling;

/// <summary>The whole way, as a request has it, against MySQL.</summary>
public class RescheduleAppointmentUseCaseTests(MySqlFixture mysql)
{
    [Fact]
    public async Task The_staff_should_move_an_appointment_and_the_old_time_should_be_free_again()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var id = await mysql.BookAsync(booking, BookingData.At(10), cancellationToken);
        var before = await mysql.LoadProfessionalAsync(booking.Company, booking.Ana, cancellationToken);

        var result = await mysql.SendAsStaffAsync(
            booking.Company,
            booking.Staff,
            new RescheduleAppointmentCommand(id, BookingData.At(14)),
            cancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var saved = await mysql.LoadAppointmentAsync(booking.Company, id, cancellationToken);
        saved.ShouldNotBeNull();
        saved.Period.Start.ShouldBe(BookingData.At(14));
        saved.Status.ShouldBe(AppointmentStatus.Pending);
        var after = await mysql.LoadProfessionalAsync(booking.Company, booking.Ana, cancellationToken);
        after!.ScheduleVersion.ShouldBe(before!.ScheduleVersion + 1);
        (await mysql.TryBookAsync(booking, BookingData.At(10), cancellationToken)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task The_new_time_may_overlap_the_old_time_of_the_same_appointment()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var id = await mysql.BookAsync(booking, BookingData.At(10), cancellationToken);

        var result = await mysql.SendAsStaffAsync(
            booking.Company,
            booking.Staff,
            new RescheduleAppointmentCommand(id, BookingData.At(10, 15)),
            cancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var saved = await mysql.LoadAppointmentAsync(booking.Company, id, cancellationToken);
        saved!.Period.Start.ShouldBe(BookingData.At(10, 15));
    }

    [Fact]
    public async Task A_time_taken_by_another_appointment_should_be_a_conflict_and_change_nothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var id = await mysql.BookAsync(booking, BookingData.At(10), cancellationToken);
        await mysql.BookAsync(booking, BookingData.At(14), cancellationToken);
        var before = await mysql.LoadProfessionalAsync(booking.Company, booking.Ana, cancellationToken);

        var result = await mysql.SendAsStaffAsync(
            booking.Company,
            booking.Staff,
            new RescheduleAppointmentCommand(id, BookingData.At(14, 15)),
            cancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Appointment.TimeNotAvailable");
        var saved = await mysql.LoadAppointmentAsync(booking.Company, id, cancellationToken);
        saved!.Period.Start.ShouldBe(BookingData.At(10));
        var after = await mysql.LoadProfessionalAsync(booking.Company, booking.Ana, cancellationToken);
        after!.ScheduleVersion.ShouldBe(before!.ScheduleVersion);
    }

    [Fact]
    public async Task A_customer_should_move_their_own_appointment_and_not_the_one_of_another()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var id = await mysql.BookAsync(booking, BookingData.At(10), cancellationToken);

        var other = await mysql.SendAsCustomerAsync(
            booking.Company,
            Guid.CreateVersion7(),
            new RescheduleAppointmentCommand(id, BookingData.At(15)),
            cancellationToken);
        var own = await mysql.SendAsCustomerAsync(
            booking.Company,
            booking.Customer,
            new RescheduleAppointmentCommand(id, BookingData.At(14)),
            cancellationToken);

        other.Error.Type.ShouldBe(ErrorType.NotFound);
        other.Error.Code.ShouldBe("Appointment.NotFound");
        own.IsSuccess.ShouldBeTrue();
        var saved = await mysql.LoadAppointmentAsync(booking.Company, id, cancellationToken);
        saved!.Period.Start.ShouldBe(BookingData.At(14));
    }

    [Fact]
    public async Task A_cancelled_appointment_should_not_be_moved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var id = await mysql.BookAsync(booking, BookingData.At(10), cancellationToken);
        await mysql.SendAsStaffAsync(booking.Company, booking.Staff, new CancelAppointmentCommand(id), cancellationToken);

        var exception = await Should.ThrowAsync<DomainException>(() => mysql.SendAsStaffAsync(
            booking.Company,
            booking.Staff,
            new RescheduleAppointmentCommand(id, BookingData.At(14)),
            cancellationToken));

        exception.Code.ShouldBe("Appointment.NotEditable");
    }

    [Fact]
    public async Task Two_appointments_moved_to_the_same_time_at_the_same_moment_should_save_only_one()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var first = await mysql.BookAsync(booking, BookingData.At(10), cancellationToken);
        var second = await mysql.BookAsync(booking, BookingData.At(15), cancellationToken);
        var before = await mysql.LoadProfessionalAsync(booking.Company, booking.Ana, cancellationToken);

        // Either the second sees the first and answers with a conflict, or both read the agenda at the same version
        // and the second save fails (RN27). In both cases it must not be saved.
        async Task<bool> TryMoveAsync(Guid id)
        {
            try
            {
                return (await mysql.SendAsStaffAsync(
                    booking.Company,
                    booking.Staff,
                    new RescheduleAppointmentCommand(id, BookingData.At(12)),
                    cancellationToken)).IsSuccess;
            }
            catch (DbUpdateException)
            {
                return false;
            }
        }

        var outcomes = await Task.WhenAll(TryMoveAsync(first), TryMoveAsync(second));

        outcomes.Count(won => won).ShouldBe(1);
        var starts = new[]
        {
            (await mysql.LoadAppointmentAsync(booking.Company, first, cancellationToken))!.Period.Start,
            (await mysql.LoadAppointmentAsync(booking.Company, second, cancellationToken))!.Period.Start,
        };
        starts.Count(start => start == BookingData.At(12)).ShouldBe(1);
        var after = await mysql.LoadProfessionalAsync(booking.Company, booking.Ana, cancellationToken);
        after!.ScheduleVersion.ShouldBe(before!.ScheduleVersion + 1);
    }
}