using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.SeedWork;
using Shouldly;
using static PlanelyApp.Domain.Tests.Scheduling.Appointments.AppointmentFixtures;

namespace PlanelyApp.Domain.Tests.Scheduling.Appointments;

public class AppointmentReminderTests
{
    // A day before the start: inside the usual window.
    private static readonly DateTime ReminderTime = StartsAt.AddHours(-24);

    [Fact]
    public void A_new_appointment_should_have_no_reminder()
    {
        Create().ReminderSentAtUtc.ShouldBeNull();
    }

    [Fact]
    public void A_confirmed_appointment_that_has_not_started_can_be_reminded()
    {
        InStatus(AppointmentStatus.Confirmed).CanBeReminded(ReminderTime).ShouldBeTrue();
    }

    [Theory]
    [InlineData(AppointmentStatus.Pending)]
    [InlineData(AppointmentStatus.InProgress)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public void An_appointment_that_is_not_confirmed_cannot_be_reminded(AppointmentStatus status)
    {
        InStatus(status).CanBeReminded(ReminderTime).ShouldBeFalse();
    }

    [Fact]
    public void An_appointment_already_reminded_cannot_be_reminded_again()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);
        appointment.MarkReminderSent(ReminderTime);

        appointment.CanBeReminded(ReminderTime.AddMinutes(1)).ShouldBeFalse();
    }

    [Fact]
    public void An_appointment_cannot_be_reminded_at_or_after_its_start()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        appointment.CanBeReminded(StartsAt).ShouldBeFalse();
        appointment.CanBeReminded(JustAfterStart).ShouldBeFalse();
    }

    [Fact]
    public void MarkReminderSent_should_record_the_time_and_raise_the_event()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);
        appointment.ClearDomainEvents();

        appointment.MarkReminderSent(ReminderTime);

        appointment.ReminderSentAtUtc.ShouldBe(ReminderTime);
        var domainEvent = appointment.DomainEvents.OfType<AppointmentReminderDue>().Single();
        domainEvent.AppointmentId.ShouldBe(appointment.Id);
        domainEvent.CompanyId.ShouldBe(CompanyId);
        domainEvent.BranchId.ShouldBe(BranchId);
        domainEvent.CustomerId.ShouldBe(CustomerId);
        domainEvent.ProfessionalId.ShouldBe(ProfessionalId);
        domainEvent.StartUtc.ShouldBe(StartsAt);
    }

    [Theory]
    [InlineData(AppointmentStatus.Pending)]
    [InlineData(AppointmentStatus.InProgress)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public void MarkReminderSent_should_refuse_an_appointment_that_is_not_confirmed(AppointmentStatus status)
    {
        var appointment = InStatus(status);
        appointment.ClearDomainEvents();

        var exception = Should.Throw<DomainException>(() => appointment.MarkReminderSent(ReminderTime));

        exception.Code.ShouldBe("Appointment.ReminderNotAllowed");
        appointment.ReminderSentAtUtc.ShouldBeNull();
        appointment.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void MarkReminderSent_should_refuse_a_second_reminder_for_the_same_time()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);
        appointment.MarkReminderSent(ReminderTime);
        var events = appointment.DomainEvents.Count;

        var exception = Should.Throw<DomainException>(() => appointment.MarkReminderSent(ReminderTime.AddHours(1)));

        exception.Code.ShouldBe("Appointment.ReminderAlreadySent");
        appointment.ReminderSentAtUtc.ShouldBe(ReminderTime);
        appointment.DomainEvents.Count.ShouldBe(events);
    }

    [Fact]
    public void MarkReminderSent_should_refuse_the_start_time_and_after()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        Should.Throw<DomainException>(() => appointment.MarkReminderSent(StartsAt))
            .Code.ShouldBe("Appointment.ReminderAfterStart");
        Should.Throw<DomainException>(() => appointment.MarkReminderSent(JustAfterStart))
            .Code.ShouldBe("Appointment.ReminderAfterStart");
        appointment.ReminderSentAtUtc.ShouldBeNull();
    }

    [Fact]
    public void MarkReminderSent_should_refuse_a_time_that_is_not_UTC()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        var exception = Should.Throw<DomainException>(
            () => appointment.MarkReminderSent(DateTime.SpecifyKind(ReminderTime, DateTimeKind.Unspecified)));

        exception.Code.ShouldBe("Appointment.TimeMustBeUtc");
    }

    [Fact]
    public void Rescheduling_should_clear_the_mark_so_the_new_time_is_reminded()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);
        appointment.MarkReminderSent(ReminderTime);

        appointment.RescheduleByStaff(StaffId, StartsAt.AddHours(3), Policy(), Now);

        appointment.ReminderSentAtUtc.ShouldBeNull();
        appointment.CanBeReminded(ReminderTime).ShouldBeTrue();
    }

    [Fact]
    public void Rescheduling_to_the_same_time_should_keep_the_mark()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);
        appointment.MarkReminderSent(ReminderTime);

        appointment.RescheduleByStaff(StaffId, StartsAt, Policy(), Now);

        appointment.ReminderSentAtUtc.ShouldBe(ReminderTime);
    }
}