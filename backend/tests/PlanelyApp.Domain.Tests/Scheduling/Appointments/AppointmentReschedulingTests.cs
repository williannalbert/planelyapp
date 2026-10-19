using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.SeedWork;
using Shouldly;
using static PlanelyApp.Domain.Tests.Scheduling.Appointments.AppointmentFixtures;

namespace PlanelyApp.Domain.Tests.Scheduling.Appointments;

public class AppointmentReschedulingTests
{
    private static readonly DateTime NewStart = Now.AddDays(3);

    // ---- By staff ----

    [Fact]
    public void RescheduleByStaff_should_move_the_period_keeping_its_length_and_the_status()
    {
        foreach (var status in new[] { AppointmentStatus.Pending, AppointmentStatus.Confirmed })
        {
            var appointment = InStatus(status);
            var length = appointment.Period.Duration;

            appointment.RescheduleByStaff(StaffId, NewStart, Policy(), Now);

            appointment.Period.Start.ShouldBe(NewStart);
            appointment.Period.End.ShouldBe(NewStart + length);
            appointment.Status.ShouldBe(status);
        }
    }

    [Fact]
    public void Rescheduling_should_record_the_event_but_not_a_status_change()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);
        var historyBefore = appointment.History.Count;

        appointment.RescheduleByStaff(StaffId, NewStart, Policy(), Now);

        appointment.History.Count.ShouldBe(historyBefore);
        var rescheduled = appointment.DomainEvents.OfType<AppointmentRescheduled>().Single();
        rescheduled.AppointmentId.ShouldBe(appointment.Id);
        rescheduled.PreviousStartUtc.ShouldBe(StartsAt);
        rescheduled.NewStartUtc.ShouldBe(NewStart);
        rescheduled.RescheduledBy.ShouldBe(StaffId);
        rescheduled.RescheduledByCustomer.ShouldBeFalse();
    }

    [Fact]
    public void RescheduleByStaff_should_work_after_the_deadline_of_the_customer()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        appointment.RescheduleByStaff(StaffId, NewStart, Policy(), StartsAt.AddHours(-1));

        appointment.Period.Start.ShouldBe(NewStart);
    }

    [Fact]
    public void Rescheduling_to_the_same_time_should_do_nothing()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        appointment.RescheduleByStaff(StaffId, StartsAt, Policy(), Now);

        appointment.Period.Start.ShouldBe(StartsAt);
        appointment.DomainEvents.OfType<AppointmentRescheduled>().ShouldBeEmpty();
    }

    // ---- By customer (RN06) ----

    [Fact]
    public void RescheduleByCustomer_before_the_deadline_should_move_it_and_record_who_did_it()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        appointment.RescheduleByCustomer(CustomerId, NewStart, Policy(), Now);

        appointment.Period.Start.ShouldBe(NewStart);
        var rescheduled = appointment.DomainEvents.OfType<AppointmentRescheduled>().Single();
        rescheduled.RescheduledBy.ShouldBe(CustomerId);
        rescheduled.RescheduledByCustomer.ShouldBeTrue();
    }

    [Fact]
    public void RescheduleByCustomer_exactly_at_the_deadline_should_be_accepted()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        appointment.RescheduleByCustomer(CustomerId, NewStart, Policy(), StartsAt.AddHours(-24));

        appointment.Period.Start.ShouldBe(NewStart);
    }

    [Fact]
    public void RescheduleByCustomer_after_the_deadline_should_be_rejected()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);
        var late = StartsAt.AddHours(-24).AddMinutes(1);

        Should.Throw<DomainException>(() => appointment.RescheduleByCustomer(CustomerId, NewStart, Policy(), late))
            .Code.ShouldBe("Appointment.CancellationDeadlinePassed");

        appointment.Period.Start.ShouldBe(StartsAt);
    }

    [Fact]
    public void RescheduleByCustomer_by_another_customer_should_be_rejected()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        Should.Throw<DomainException>(() =>
                appointment.RescheduleByCustomer(Guid.CreateVersion7(), NewStart, Policy(), Now))
            .Code.ShouldBe("Appointment.NotTheCustomer");
    }

    [Fact]
    public void The_deadline_should_be_counted_from_the_current_start_after_a_reschedule()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);
        appointment.RescheduleByStaff(StaffId, NewStart, Policy(), Now);

        // Before the move this instant would have been past the deadline; now the start is one day later.
        var instant = StartsAt.AddHours(-1);

        appointment.RescheduleByCustomer(CustomerId, NewStart.AddDays(1), Policy(), instant);

        appointment.Period.Start.ShouldBe(NewStart.AddDays(1));
    }

    // ---- RN03 ----

    [Fact]
    public void The_new_start_should_respect_the_minimum_notice_RN03()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        Should.Throw<DomainException>(() =>
                appointment.RescheduleByStaff(StaffId, Now.AddHours(2).AddMinutes(-1), Policy(), Now))
            .Code.ShouldBe("Appointment.MinimumNoticeNotMet");

        appointment.RescheduleByStaff(StaffId, Now.AddHours(2), Policy(), Now);

        appointment.Period.Start.ShouldBe(Now.AddHours(2));
    }

    [Fact]
    public void The_new_start_should_respect_the_booking_window_RN03()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        Should.Throw<DomainException>(() =>
                appointment.RescheduleByStaff(StaffId, Now.AddDays(60).AddMinutes(1), Policy(), Now))
            .Code.ShouldBe("Appointment.BeyondBookingWindow");

        appointment.Period.Start.ShouldBe(StartsAt);

        appointment.RescheduleByStaff(StaffId, Now.AddDays(60), Policy(), Now);

        appointment.Period.Start.ShouldBe(Now.AddDays(60));
    }

    // ---- Invalid situations ----

    [Fact]
    public void Rescheduling_after_the_appointment_started_or_ended_should_be_rejected()
    {
        foreach (var status in new[]
                 {
                     AppointmentStatus.InProgress, AppointmentStatus.Completed,
                     AppointmentStatus.Cancelled, AppointmentStatus.NoShow,
                 })
        {
            var appointment = InStatus(status);

            Should.Throw<DomainException>(() => appointment.RescheduleByStaff(StaffId, NewStart, Policy(), Now))
                .Code.ShouldBe("Appointment.NotEditable");
        }
    }

    [Fact]
    public void Rescheduling_to_a_start_that_is_not_UTC_should_be_rejected()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);
        var unspecified = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Unspecified);

        Should.Throw<DomainException>(() => appointment.RescheduleByStaff(StaffId, unspecified, Policy(), Now))
            .Code.ShouldBe("DateTimeRange.MustBeUtc");

        appointment.Period.Start.ShouldBe(StartsAt);
    }

    [Fact]
    public void Rescheduling_without_an_actor_or_with_a_time_that_is_not_UTC_should_be_rejected()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);
        var unspecified = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Unspecified);

        Should.Throw<DomainException>(() => appointment.RescheduleByStaff(Guid.Empty, NewStart, Policy(), Now))
            .Code.ShouldBe("Appointment.ActorRequired");
        Should.Throw<DomainException>(() => appointment.RescheduleByStaff(StaffId, NewStart, Policy(), unspecified))
            .Code.ShouldBe("Appointment.TimeMustBeUtc");
    }
}