using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.Scheduling.Pricing;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Scheduling.Appointments;

public class AppointmentTransitionTests
{
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly Guid BranchId = Guid.CreateVersion7();
    private static readonly Guid CustomerId = Guid.CreateVersion7();
    private static readonly Guid ProfessionalId = Guid.CreateVersion7();
    private static readonly Guid CreatedBy = Guid.CreateVersion7();
    private static readonly Guid StaffId = Guid.CreateVersion7();

    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime StartsAt = Now.AddDays(2);
    private static readonly DateTime JustAfterStart = StartsAt.AddMinutes(5);

    private static BookingPolicy Policy(bool requiresConfirmation = false, int cancellationDeadlineHours = 24) =>
        BookingPolicy.Create(2, 60, cancellationDeadlineHours, 0, 30, requiresConfirmation);

    private static NewAppointmentItem FixedItem() =>
        new(
            ServiceOffering.CreateWithFixedPrice(CompanyId, "Haircut", TimeSpan.FromMinutes(45), Money.From(80m)),
            TimeSpan.FromMinutes(45),
            PriceQuote.Fixed(Money.From(80m)));

    private static NewAppointmentItem OnRequestItem() =>
        new(
            ServiceOffering.CreateOnRequest(CompanyId, "Custom tattoo", TimeSpan.FromHours(3)),
            TimeSpan.FromHours(3),
            PriceQuote.OnRequest());

    private static Appointment CreatePending(IEnumerable<NewAppointmentItem>? items = null) =>
        Appointment.Create(
            CompanyId,
            BranchId,
            CustomerId,
            ProfessionalId,
            StartsAt,
            items ?? new[] { FixedItem() },
            Policy(requiresConfirmation: true),
            Now,
            CreatedBy);

    // Builds an appointment that goes through the real transitions until it reaches the status.
    private static Appointment InStatus(AppointmentStatus status)
    {
        var appointment = CreatePending();

        switch (status)
        {
            case AppointmentStatus.Pending:
                break;
            case AppointmentStatus.Confirmed:
                appointment.Confirm(StaffId, Now);
                break;
            case AppointmentStatus.InProgress:
                appointment.Confirm(StaffId, Now);
                appointment.Start(StaffId, StartsAt);
                break;
            case AppointmentStatus.Completed:
                appointment.Confirm(StaffId, Now);
                appointment.Start(StaffId, StartsAt);
                appointment.Complete(StaffId, StartsAt.AddMinutes(45));
                break;
            case AppointmentStatus.Cancelled:
                appointment.CancelByStaff(StaffId, Now);
                break;
            case AppointmentStatus.NoShow:
                appointment.Confirm(StaffId, Now);
                appointment.MarkAsNoShow(StaffId, JustAfterStart);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        appointment.Status.ShouldBe(status);

        return appointment;
    }

    // ---- State machine ----

    [Fact]
    public void CanMoveTo_should_follow_the_state_diagram()
    {
        var expected = new Dictionary<AppointmentStatus, AppointmentStatus[]>
        {
            [AppointmentStatus.Pending] = [AppointmentStatus.Confirmed, AppointmentStatus.Cancelled],
            [AppointmentStatus.Confirmed] = [AppointmentStatus.InProgress, AppointmentStatus.NoShow, AppointmentStatus.Cancelled],
            [AppointmentStatus.InProgress] = [AppointmentStatus.Completed, AppointmentStatus.Cancelled],
            [AppointmentStatus.Completed] = [],
            [AppointmentStatus.Cancelled] = [],
            [AppointmentStatus.NoShow] = [],
        };

        foreach (var from in Enum.GetValues<AppointmentStatus>())
        {
            var appointment = InStatus(from);

            foreach (var to in Enum.GetValues<AppointmentStatus>())
            {
                appointment.CanMoveTo(to).ShouldBe(expected[from].Contains(to), $"{from} -> {to}");
            }
        }
    }

    [Fact]
    public void OccupiesSchedule_should_be_true_only_while_the_appointment_is_active()
    {
        InStatus(AppointmentStatus.Pending).OccupiesSchedule.ShouldBeTrue();
        InStatus(AppointmentStatus.Confirmed).OccupiesSchedule.ShouldBeTrue();
        InStatus(AppointmentStatus.InProgress).OccupiesSchedule.ShouldBeTrue();
        InStatus(AppointmentStatus.Completed).OccupiesSchedule.ShouldBeFalse();
        InStatus(AppointmentStatus.Cancelled).OccupiesSchedule.ShouldBeFalse();
        InStatus(AppointmentStatus.NoShow).OccupiesSchedule.ShouldBeFalse();
    }

    [Fact]
    public void The_history_should_follow_the_whole_life_of_the_appointment_RN08()
    {
        var appointment = InStatus(AppointmentStatus.Completed);

        appointment.History.Select(change => change.From)
            .ShouldBe(new AppointmentStatus?[]
            {
                null, AppointmentStatus.Pending, AppointmentStatus.Confirmed, AppointmentStatus.InProgress,
            });
        appointment.History.Select(change => change.To)
            .ShouldBe(
            [
                AppointmentStatus.Pending, AppointmentStatus.Confirmed, AppointmentStatus.InProgress, AppointmentStatus.Completed,
            ]);
    }

    [Fact]
    public void An_invalid_transition_should_be_rejected_and_leave_the_appointment_untouched()
    {
        var appointment = InStatus(AppointmentStatus.Pending);

        Should.Throw<DomainException>(() => appointment.Complete(StaffId, Now))
            .Code.ShouldBe("Appointment.InvalidStatusTransition");

        appointment.Status.ShouldBe(AppointmentStatus.Pending);
        appointment.History.Count.ShouldBe(1);
    }

    [Fact]
    public void A_change_without_an_actor_or_with_a_time_that_is_not_UTC_should_be_rejected()
    {
        var appointment = InStatus(AppointmentStatus.Pending);
        var unspecified = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Unspecified);

        Should.Throw<DomainException>(() => appointment.Confirm(Guid.Empty, Now))
            .Code.ShouldBe("Appointment.ActorRequired");
        Should.Throw<DomainException>(() => appointment.Confirm(StaffId, unspecified))
            .Code.ShouldBe("Appointment.TimeMustBeUtc");

        appointment.Status.ShouldBe(AppointmentStatus.Pending);
    }

    // ---- Confirm ----

    [Fact]
    public void Confirm_should_move_a_pending_appointment_to_confirmed()
    {
        var appointment = InStatus(AppointmentStatus.Pending);

        appointment.Confirm(StaffId, Now.AddHours(1));

        appointment.Status.ShouldBe(AppointmentStatus.Confirmed);
        var change = appointment.History.Last();
        change.From.ShouldBe(AppointmentStatus.Pending);
        change.To.ShouldBe(AppointmentStatus.Confirmed);
        change.ChangedBy.ShouldBe(StaffId);
        change.OccurredAtUtc.ShouldBe(Now.AddHours(1));
    }

    [Fact]
    public void Confirm_should_record_the_AppointmentConfirmed_event_with_the_total()
    {
        var appointment = InStatus(AppointmentStatus.Pending);

        appointment.Confirm(StaffId, Now);

        var confirmed = appointment.DomainEvents.OfType<AppointmentConfirmed>().Single();
        confirmed.AppointmentId.ShouldBe(appointment.Id);
        confirmed.CustomerId.ShouldBe(CustomerId);
        confirmed.StartUtc.ShouldBe(StartsAt);
        confirmed.TotalPrice.ShouldBe(Money.From(80m));
    }

    [Fact]
    public void Confirm_with_a_price_still_pending_should_be_rejected_RN19()
    {
        var appointment = CreatePending(new[] { OnRequestItem() });

        Should.Throw<DomainException>(() => appointment.Confirm(StaffId, Now))
            .Code.ShouldBe("Appointment.PriceStillPending");

        appointment.Status.ShouldBe(AppointmentStatus.Pending);
        appointment.History.Count.ShouldBe(1);
    }

    // ---- Start and Complete ----

    [Fact]
    public void Start_should_move_a_confirmed_appointment_to_in_progress()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        appointment.Start(StaffId, StartsAt);

        appointment.Status.ShouldBe(AppointmentStatus.InProgress);
        appointment.History.Last().To.ShouldBe(AppointmentStatus.InProgress);
    }

    [Fact]
    public void Start_on_a_pending_appointment_should_be_rejected()
    {
        var appointment = InStatus(AppointmentStatus.Pending);

        Should.Throw<DomainException>(() => appointment.Start(StaffId, StartsAt))
            .Code.ShouldBe("Appointment.InvalidStatusTransition");
    }

    [Fact]
    public void Complete_should_move_an_appointment_in_progress_to_completed()
    {
        var appointment = InStatus(AppointmentStatus.InProgress);

        appointment.Complete(StaffId, StartsAt.AddMinutes(45));

        appointment.Status.ShouldBe(AppointmentStatus.Completed);
    }

    [Fact]
    public void Complete_on_a_confirmed_appointment_should_be_rejected()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        Should.Throw<DomainException>(() => appointment.Complete(StaffId, StartsAt))
            .Code.ShouldBe("Appointment.InvalidStatusTransition");
    }

    // ---- No show ----

    [Fact]
    public void MarkAsNoShow_after_the_start_should_move_a_confirmed_appointment_to_no_show()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        appointment.MarkAsNoShow(StaffId, JustAfterStart);

        appointment.Status.ShouldBe(AppointmentStatus.NoShow);
    }

    [Fact]
    public void MarkAsNoShow_exactly_at_the_start_should_be_accepted()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        appointment.MarkAsNoShow(StaffId, StartsAt);

        appointment.Status.ShouldBe(AppointmentStatus.NoShow);
    }

    [Fact]
    public void MarkAsNoShow_before_the_start_should_be_rejected()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        Should.Throw<DomainException>(() => appointment.MarkAsNoShow(StaffId, StartsAt.AddMinutes(-1)))
            .Code.ShouldBe("Appointment.NoShowBeforeStart");

        appointment.Status.ShouldBe(AppointmentStatus.Confirmed);
    }

    [Fact]
    public void MarkAsNoShow_on_a_pending_appointment_should_be_rejected()
    {
        var appointment = InStatus(AppointmentStatus.Pending);

        Should.Throw<DomainException>(() => appointment.MarkAsNoShow(StaffId, JustAfterStart))
            .Code.ShouldBe("Appointment.InvalidStatusTransition");
    }

    // ---- Cancel by staff ----

    [Fact]
    public void CancelByStaff_should_cancel_pending_and_confirmed_appointments()
    {
        foreach (var status in new[] { AppointmentStatus.Pending, AppointmentStatus.Confirmed })
        {
            var appointment = InStatus(status);

            appointment.CancelByStaff(StaffId, Now);

            appointment.Status.ShouldBe(AppointmentStatus.Cancelled);
        }
    }

    [Fact]
    public void CancelByStaff_should_record_the_reason_and_the_event()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        appointment.CancelByStaff(StaffId, Now, "  Professional is ill  ");

        var change = appointment.History.Last();
        change.From.ShouldBe(AppointmentStatus.Confirmed);
        change.To.ShouldBe(AppointmentStatus.Cancelled);
        change.ChangedBy.ShouldBe(StaffId);
        change.Reason.ShouldBe("Professional is ill");

        var cancelled = appointment.DomainEvents.OfType<AppointmentCancelled>().Single();
        cancelled.AppointmentId.ShouldBe(appointment.Id);
        cancelled.Reason.ShouldBe("Professional is ill");
        cancelled.CancelledByCustomer.ShouldBeFalse();
    }

    [Fact]
    public void CancelByStaff_should_work_after_the_deadline_of_the_customer()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        appointment.CancelByStaff(StaffId, StartsAt.AddHours(-1));

        appointment.Status.ShouldBe(AppointmentStatus.Cancelled);
    }

    [Fact]
    public void CancelByStaff_with_an_invalid_reason_should_leave_the_appointment_untouched()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        Should.Throw<DomainException>(() => appointment.CancelByStaff(StaffId, Now, new string('a', 501)))
            .Code.ShouldBe("StatusChange.ReasonInvalid");

        appointment.Status.ShouldBe(AppointmentStatus.Confirmed);
        appointment.History.Count.ShouldBe(2);
        appointment.DomainEvents.OfType<AppointmentCancelled>().ShouldBeEmpty();
    }
    [Fact]
    public void CancelByStaff_after_the_service_ended_should_be_rejected()
    {
        foreach (var status in new[]
                 {
                     AppointmentStatus.Completed, AppointmentStatus.Cancelled, AppointmentStatus.NoShow,
                 })
        {
            var appointment = InStatus(status);

            Should.Throw<DomainException>(() => appointment.CancelByStaff(StaffId, JustAfterStart))
                .Code.ShouldBe("Appointment.InvalidStatusTransition");
        }
    }

    // ---- Cancel by customer (RN06) ----

    [Fact]
    public void CancelByCustomer_before_the_deadline_should_cancel_and_record_who_did_it()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        appointment.CancelByCustomer(CustomerId, Policy(), Now, "Changed my mind");

        appointment.Status.ShouldBe(AppointmentStatus.Cancelled);
        var change = appointment.History.Last();
        change.ChangedBy.ShouldBe(CustomerId);
        change.Reason.ShouldBe("Changed my mind");
        appointment.DomainEvents.OfType<AppointmentCancelled>().Single().CancelledByCustomer.ShouldBeTrue();
    }

    [Fact]
    public void CancelByCustomer_exactly_at_the_deadline_should_be_accepted()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        appointment.CancelByCustomer(CustomerId, Policy(cancellationDeadlineHours: 24), StartsAt.AddHours(-24));

        appointment.Status.ShouldBe(AppointmentStatus.Cancelled);
    }

    [Fact]
    public void CancelByCustomer_after_the_deadline_should_be_rejected()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);
        var late = StartsAt.AddHours(-24).AddMinutes(1);

        Should.Throw<DomainException>(() => appointment.CancelByCustomer(CustomerId, Policy(cancellationDeadlineHours: 24), late))
            .Code.ShouldBe("Appointment.CancellationDeadlinePassed");

        appointment.Status.ShouldBe(AppointmentStatus.Confirmed);
    }

    [Fact]
    public void CancelByCustomer_by_another_customer_should_be_rejected()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        Should.Throw<DomainException>(() => appointment.CancelByCustomer(Guid.CreateVersion7(), Policy(), Now))
            .Code.ShouldBe("Appointment.NotTheCustomer");
    }

    [Fact]
    public void CancelByCustomer_on_an_appointment_that_cannot_be_cancelled_should_be_rejected()
    {
        var appointment = InStatus(AppointmentStatus.Cancelled);

        Should.Throw<DomainException>(() => appointment.CancelByCustomer(CustomerId, Policy(), Now))
            .Code.ShouldBe("Appointment.InvalidStatusTransition");
    }
    [Fact]
    public void CancelByStaff_while_in_progress_should_cancel_for_emergencies()
    {
        var appointment = InStatus(AppointmentStatus.InProgress);

        appointment.CancelByStaff(StaffId, JustAfterStart, "Power outage");

        appointment.Status.ShouldBe(AppointmentStatus.Cancelled);
        var change = appointment.History.Last();
        change.From.ShouldBe(AppointmentStatus.InProgress);
        change.To.ShouldBe(AppointmentStatus.Cancelled);
        change.Reason.ShouldBe("Power outage");
        appointment.DomainEvents.OfType<AppointmentCancelled>().Single().CancelledByCustomer.ShouldBeFalse();
    }

    [Fact]
    public void CancelByCustomer_while_in_progress_should_be_rejected_even_with_no_deadline()
    {
        var appointment = InStatus(AppointmentStatus.InProgress);
        var noDeadline = Policy(cancellationDeadlineHours: 0);

        // The service started early: without the explicit check, this instant would still be "before the deadline".
        Should.Throw<DomainException>(() => appointment.CancelByCustomer(CustomerId, noDeadline, StartsAt.AddMinutes(-1)))
            .Code.ShouldBe("Appointment.CancellationDeadlinePassed");

        appointment.Status.ShouldBe(AppointmentStatus.InProgress);
    }
}