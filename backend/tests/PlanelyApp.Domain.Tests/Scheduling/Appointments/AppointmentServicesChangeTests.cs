using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.Scheduling.Pricing;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;
using static PlanelyApp.Domain.Tests.Scheduling.Appointments.AppointmentFixtures;

namespace PlanelyApp.Domain.Tests.Scheduling.Appointments;

public class AppointmentServicesChangeTests
{
    // ---- Adding ----

    [Fact]
    public void AddItemByStaff_should_add_the_service_and_extend_the_period_and_the_total()
    {
        foreach (var status in new[] { AppointmentStatus.Pending, AppointmentStatus.Confirmed })
        {
            var appointment = InStatus(status);

            appointment.AddItemByStaff(StaffId, FixedItem(minutes: 30, price: 40m));

            appointment.Items.Count.ShouldBe(2);
            appointment.ServicesDuration.ShouldBe(TimeSpan.FromMinutes(75));
            appointment.Period.Start.ShouldBe(StartsAt);
            appointment.Period.End.ShouldBe(StartsAt.AddMinutes(75));
            appointment.TotalPrice.ShouldBe(Money.From(120m));
            appointment.Status.ShouldBe(status);
        }
    }

    [Fact]
    public void AddItemByStaff_should_copy_the_service_data_into_the_new_item()
    {
        var appointment = InStatus(AppointmentStatus.Pending);

        appointment.AddItemByStaff(StaffId, FixedItem(minutes: 30, price: 40m));

        var item = appointment.Items.Last();
        item.ServiceName.ShouldBe("Haircut");
        item.Duration.ShouldBe(TimeSpan.FromMinutes(30));
        item.FinalPrice.ShouldBe(Money.From(40m));
    }

    [Fact]
    public void Adding_a_service_should_keep_the_interval_the_appointment_was_created_with()
    {
        var appointment = Create(interval: 15);
        appointment.Period.End.ShouldBe(StartsAt.AddMinutes(45 + 15));

        appointment.AddItemByStaff(StaffId, FixedItem(minutes: 30));

        appointment.Period.End.ShouldBe(StartsAt.AddMinutes(75 + 15));
    }

    [Fact]
    public void Adding_a_service_should_record_the_event_with_the_resulting_state()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        appointment.AddItemByStaff(StaffId, FixedItem(minutes: 30, price: 40m));

        var changed = appointment.DomainEvents.OfType<AppointmentServicesChanged>().Single();
        changed.AppointmentId.ShouldBe(appointment.Id);
        changed.StartUtc.ShouldBe(StartsAt);
        changed.EndUtc.ShouldBe(StartsAt.AddMinutes(75));
        changed.TotalPrice.ShouldBe(Money.From(120m));
        changed.ChangedBy.ShouldBe(StaffId);
        changed.ChangedByCustomer.ShouldBeFalse();
    }

    [Fact]
    public void Adding_a_service_on_request_to_a_pending_appointment_should_leave_the_total_pending()
    {
        var appointment = InStatus(AppointmentStatus.Pending);

        appointment.AddItemByStaff(StaffId, OnRequestItem());

        appointment.TotalPrice.ShouldBeNull();
        appointment.HasPendingPrice.ShouldBeTrue();
        appointment.Status.ShouldBe(AppointmentStatus.Pending);
    }

    [Fact]
    public void Adding_a_service_on_request_to_a_confirmed_appointment_should_be_rejected_RN19()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        Should.Throw<DomainException>(() => appointment.AddItemByStaff(StaffId, OnRequestItem()))
            .Code.ShouldBe("Appointment.OnRequestItemRequiresPending");

        appointment.Items.Count.ShouldBe(1);
        appointment.TotalPrice.ShouldBe(Money.From(80m));
    }

    [Fact]
    public void Adding_an_invalid_service_should_be_rejected_and_leave_the_appointment_untouched()
    {
        var appointment = InStatus(AppointmentStatus.Pending);
        var periodBefore = appointment.Period;

        var foreign = new NewAppointmentItem(
            ServiceOffering.CreateWithFixedPrice(Guid.CreateVersion7(), "Haircut", TimeSpan.FromMinutes(45), Money.From(80m)),
            TimeSpan.FromMinutes(45),
            PriceQuote.Fixed(Money.From(80m)));
        var mismatch = new NewAppointmentItem(
            ServiceOffering.CreateOnRequest(CompanyId, "Custom tattoo", TimeSpan.FromHours(1)),
            TimeSpan.FromHours(1),
            PriceQuote.Fixed(Money.From(10m)));
        var badDuration = new NewAppointmentItem(
            ServiceOffering.CreateWithFixedPrice(CompanyId, "Haircut", TimeSpan.FromMinutes(45), Money.From(80m)),
            TimeSpan.FromSeconds(90),
            PriceQuote.Fixed(Money.From(80m)));

        Should.Throw<DomainException>(() => appointment.AddItemByStaff(StaffId, foreign))
            .Code.ShouldBe("Appointment.ServiceFromAnotherCompany");
        Should.Throw<DomainException>(() => appointment.AddItemByStaff(StaffId, mismatch))
            .Code.ShouldBe("Appointment.PriceDoesNotMatchService");
        Should.Throw<DomainException>(() => appointment.AddItemByStaff(StaffId, badDuration))
            .Code.ShouldBe("Appointment.ItemDurationInvalid");

        appointment.Items.Count.ShouldBe(1);
        appointment.Period.ShouldBe(periodBefore);
        appointment.DomainEvents.OfType<AppointmentServicesChanged>().ShouldBeEmpty();
    }

    [Fact]
    public void Adding_a_service_after_the_appointment_started_or_ended_should_be_rejected()
    {
        foreach (var status in new[]
                 {
                     AppointmentStatus.InProgress, AppointmentStatus.Completed,
                     AppointmentStatus.Cancelled, AppointmentStatus.NoShow,
                 })
        {
            var appointment = InStatus(status);

            Should.Throw<DomainException>(() => appointment.AddItemByStaff(StaffId, FixedItem()))
                .Code.ShouldBe("Appointment.NotEditable");
        }
    }

    [Fact]
    public void AddItemByStaff_without_an_actor_should_be_rejected()
    {
        var appointment = InStatus(AppointmentStatus.Pending);

        Should.Throw<DomainException>(() => appointment.AddItemByStaff(Guid.Empty, FixedItem()))
            .Code.ShouldBe("Appointment.ActorRequired");
    }

    // ---- Adding by the customer (RN06) ----

    [Fact]
    public void AddItemByCustomer_before_the_deadline_should_add_the_service_and_record_who_did_it()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        appointment.AddItemByCustomer(CustomerId, FixedItem(minutes: 30, price: 40m), Policy(), Now);

        appointment.Items.Count.ShouldBe(2);
        var changed = appointment.DomainEvents.OfType<AppointmentServicesChanged>().Single();
        changed.ChangedBy.ShouldBe(CustomerId);
        changed.ChangedByCustomer.ShouldBeTrue();
    }

    [Fact]
    public void AddItemByCustomer_after_the_deadline_should_be_rejected_but_the_company_can_still_add()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);
        var late = StartsAt.AddHours(-24).AddMinutes(1);

        Should.Throw<DomainException>(() =>
                appointment.AddItemByCustomer(CustomerId, FixedItem(), Policy(), late))
            .Code.ShouldBe("Appointment.CancellationDeadlinePassed");
        appointment.Items.Count.ShouldBe(1);

        appointment.AddItemByStaff(StaffId, FixedItem());

        appointment.Items.Count.ShouldBe(2);
    }

    [Fact]
    public void AddItemByCustomer_by_another_customer_should_be_rejected()
    {
        var appointment = InStatus(AppointmentStatus.Confirmed);

        Should.Throw<DomainException>(() =>
                appointment.AddItemByCustomer(Guid.CreateVersion7(), FixedItem(), Policy(), Now))
            .Code.ShouldBe("Appointment.NotTheCustomer");
    }

    // ---- Removing ----

    [Fact]
    public void RemoveItemByStaff_should_remove_the_service_and_shrink_the_period_and_the_total()
    {
        foreach (var status in new[] { AppointmentStatus.Pending, AppointmentStatus.Confirmed })
        {
            var appointment = Create(new[] { FixedItem(minutes: 45, price: 80m), FixedItem(minutes: 30, price: 40m) });
            if (status == AppointmentStatus.Confirmed)
            {
                appointment.Confirm(StaffId, Now);
            }

            appointment.RemoveItemByStaff(StaffId, appointment.Items.Last().Id);

            appointment.Items.Count.ShouldBe(1);
            appointment.ServicesDuration.ShouldBe(TimeSpan.FromMinutes(45));
            appointment.Period.Start.ShouldBe(StartsAt);
            appointment.Period.End.ShouldBe(StartsAt.AddMinutes(45));
            appointment.TotalPrice.ShouldBe(Money.From(80m));
            appointment.Status.ShouldBe(status);
        }
    }

    [Fact]
    public void Removing_a_service_should_record_the_event_with_the_resulting_state()
    {
        var appointment = Create(new[] { FixedItem(minutes: 45, price: 80m), FixedItem(minutes: 30, price: 40m) });

        appointment.RemoveItemByStaff(StaffId, appointment.Items.Last().Id);

        var changed = appointment.DomainEvents.OfType<AppointmentServicesChanged>().Single();
        changed.EndUtc.ShouldBe(StartsAt.AddMinutes(45));
        changed.TotalPrice.ShouldBe(Money.From(80m));
    }

    [Fact]
    public void Removing_the_only_service_on_request_should_make_the_total_known()
    {
        var appointment = Create(new[] { FixedItem(price: 60m), OnRequestItem() });
        appointment.TotalPrice.ShouldBeNull();

        appointment.RemoveItemByStaff(StaffId, appointment.Items.Single(item => item.IsOnRequest).Id);

        appointment.TotalPrice.ShouldBe(Money.From(60m));
        appointment.HasPendingPrice.ShouldBeFalse();
        appointment.Status.ShouldBe(AppointmentStatus.Pending);
    }

    [Fact]
    public void Removing_the_last_service_should_be_rejected()
    {
        var appointment = InStatus(AppointmentStatus.Pending);

        Should.Throw<DomainException>(() => appointment.RemoveItemByStaff(StaffId, appointment.Items.Single().Id))
            .Code.ShouldBe("Appointment.ItemsRequired");

        appointment.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void Removing_an_unknown_service_should_be_rejected()
    {
        var appointment = Create(new[] { FixedItem(), FixedItem() });

        Should.Throw<DomainException>(() => appointment.RemoveItemByStaff(StaffId, Guid.CreateVersion7()))
            .Code.ShouldBe("Appointment.ItemNotFound");

        appointment.Items.Count.ShouldBe(2);
    }

    [Fact]
    public void Removing_a_service_after_the_appointment_started_or_ended_should_be_rejected()
    {
        foreach (var status in new[]
                 {
                     AppointmentStatus.InProgress, AppointmentStatus.Completed,
                     AppointmentStatus.Cancelled, AppointmentStatus.NoShow,
                 })
        {
            var appointment = InStatus(status);

            Should.Throw<DomainException>(() =>
                    appointment.RemoveItemByStaff(StaffId, appointment.Items.Single().Id))
                .Code.ShouldBe("Appointment.NotEditable");
        }
    }

    // ---- Removing by the customer (RN06) ----

    [Fact]
    public void RemoveItemByCustomer_before_the_deadline_should_remove_it_and_record_who_did_it()
    {
        var appointment = Create(new[] { FixedItem(), FixedItem() });

        appointment.RemoveItemByCustomer(CustomerId, appointment.Items.Last().Id, Policy(), Now);

        appointment.Items.Count.ShouldBe(1);
        var changed = appointment.DomainEvents.OfType<AppointmentServicesChanged>().Single();
        changed.ChangedBy.ShouldBe(CustomerId);
        changed.ChangedByCustomer.ShouldBeTrue();
    }

    [Fact]
    public void RemoveItemByCustomer_after_the_deadline_should_be_rejected_but_the_company_can_still_remove()
    {
        var appointment = Create(new[] { FixedItem(), FixedItem() });
        var late = StartsAt.AddHours(-24).AddMinutes(1);

        Should.Throw<DomainException>(() =>
                appointment.RemoveItemByCustomer(CustomerId, appointment.Items.Last().Id, Policy(), late))
            .Code.ShouldBe("Appointment.CancellationDeadlinePassed");
        appointment.Items.Count.ShouldBe(2);

        appointment.RemoveItemByStaff(StaffId, appointment.Items.Last().Id);

        appointment.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void RemoveItemByCustomer_by_another_customer_should_be_rejected()
    {
        var appointment = Create(new[] { FixedItem(), FixedItem() });

        Should.Throw<DomainException>(() =>
                appointment.RemoveItemByCustomer(Guid.CreateVersion7(), appointment.Items.Last().Id, Policy(), Now))
            .Code.ShouldBe("Appointment.NotTheCustomer");
    }

    // ---- Together ----

    [Fact]
    public void Adding_and_removing_should_leave_the_period_as_it_was()
    {
        var appointment = Create(interval: 15);
        var periodBefore = appointment.Period;

        appointment.AddItemByStaff(StaffId, FixedItem(minutes: 30));
        appointment.RemoveItemByStaff(StaffId, appointment.Items.Last().Id);

        appointment.Period.ShouldBe(periodBefore);
        appointment.DomainEvents.OfType<AppointmentServicesChanged>().Count().ShouldBe(2);
    }
}