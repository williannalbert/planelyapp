using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;
using static PlanelyApp.Domain.Tests.Scheduling.Appointments.AppointmentFixtures;

namespace PlanelyApp.Domain.Tests.Scheduling.Appointments;

public class AppointmentOnRequestPriceTests
{
    [Fact]
    public void SetItemPrice_should_set_the_price_of_the_item_and_the_total()
    {
        var appointment = Create(new[] { OnRequestItem() });
        var item = appointment.Items.Single();

        appointment.SetItemPrice(item.Id, Money.From(500m));

        item.ListPrice.ShouldBe(Money.From(500m));
        item.FinalPrice.ShouldBe(Money.From(500m));
        item.Discount.ShouldBe(Money.Zero);
        item.IsOnRequest.ShouldBeTrue();
        item.HasPendingPrice.ShouldBeFalse();
        appointment.HasPendingPrice.ShouldBeFalse();
        appointment.TotalPrice.ShouldBe(Money.From(500m));
        appointment.Status.ShouldBe(AppointmentStatus.Pending);
    }

    [Fact]
    public void SetItemPrice_should_add_to_the_prices_of_the_other_items()
    {
        var appointment = Create(new[] { FixedItem(price: 60m), OnRequestItem() });
        appointment.TotalPrice.ShouldBeNull();

        appointment.SetItemPrice(appointment.Items.Single(item => item.IsOnRequest).Id, Money.From(500m));

        appointment.TotalPrice.ShouldBe(Money.From(560m));
    }

    [Fact]
    public void SetItemPrice_again_should_replace_the_price_while_pending()
    {
        var appointment = Create(new[] { OnRequestItem() });
        var item = appointment.Items.Single();
        appointment.SetItemPrice(item.Id, Money.From(500m));

        appointment.SetItemPrice(item.Id, Money.From(450m));

        appointment.TotalPrice.ShouldBe(Money.From(450m));
    }

    // ---- Duration (RN19) ----

    [Fact]
    public void SetItemPrice_without_a_new_duration_should_keep_the_period()
    {
        var appointment = Create(new[] { OnRequestItem(hours: 3) });
        var periodBefore = appointment.Period;

        appointment.SetItemPrice(appointment.Items.Single().Id, Money.From(500m));

        appointment.Period.ShouldBe(periodBefore);
    }

    [Fact]
    public void SetItemPrice_with_a_new_duration_should_change_the_item_and_the_period()
    {
        var appointment = Create(new[] { OnRequestItem(hours: 3) });
        var item = appointment.Items.Single();

        appointment.SetItemPrice(item.Id, Money.From(500m), TimeSpan.FromHours(4));

        item.Duration.ShouldBe(TimeSpan.FromHours(4));
        appointment.ServicesDuration.ShouldBe(TimeSpan.FromHours(4));
        appointment.Period.Start.ShouldBe(StartsAt);
        appointment.Period.End.ShouldBe(StartsAt.AddHours(4));
    }

    [Fact]
    public void A_new_duration_should_keep_the_interval_the_appointment_was_created_with()
    {
        var appointment = Create(new[] { OnRequestItem(hours: 3) }, interval: 15);
        appointment.Period.End.ShouldBe(StartsAt.AddHours(3).AddMinutes(15));

        appointment.SetItemPrice(appointment.Items.Single().Id, Money.From(500m), TimeSpan.FromHours(4));

        appointment.Period.End.ShouldBe(StartsAt.AddHours(4).AddMinutes(15));
    }

    [Fact]
    public void An_invalid_new_duration_should_be_rejected_and_leave_the_appointment_untouched()
    {
        var appointment = Create(new[] { OnRequestItem(hours: 3) });
        var item = appointment.Items.Single();
        var periodBefore = appointment.Period;

        foreach (var duration in new[] { TimeSpan.Zero, TimeSpan.FromSeconds(90), TimeSpan.FromHours(25) })
        {
            Should.Throw<DomainException>(() => appointment.SetItemPrice(item.Id, Money.From(500m), duration))
                .Code.ShouldBe("Appointment.ItemDurationInvalid");
        }

        item.HasPendingPrice.ShouldBeTrue();
        item.Duration.ShouldBe(TimeSpan.FromHours(3));
        appointment.Period.ShouldBe(periodBefore);
    }

    // ---- Invalid situations ----

    [Fact]
    public void SetItemPrice_on_a_fixed_price_item_should_be_rejected()
    {
        var appointment = Create(new[] { FixedItem() });

        Should.Throw<DomainException>(() =>
                appointment.SetItemPrice(appointment.Items.Single().Id, Money.From(10m)))
            .Code.ShouldBe("Appointment.ItemHasFixedPrice");
    }

    [Fact]
    public void SetItemPrice_for_an_unknown_item_should_be_rejected()
    {
        var appointment = Create(new[] { OnRequestItem() });

        Should.Throw<DomainException>(() => appointment.SetItemPrice(Guid.CreateVersion7(), Money.From(500m)))
            .Code.ShouldBe("Appointment.ItemNotFound");
    }

    [Fact]
    public void SetItemPrice_when_the_appointment_is_not_pending_should_be_rejected()
    {
        var confirmed = Create(new[] { FixedItem() }, requiresConfirmation: false);
        var cancelled = Create(new[] { OnRequestItem() });
        cancelled.CancelByStaff(StaffId, Now);

        Should.Throw<DomainException>(() =>
                confirmed.SetItemPrice(confirmed.Items.Single().Id, Money.From(10m)))
            .Code.ShouldBe("Appointment.PriceCanOnlyBeSetWhilePending");
        Should.Throw<DomainException>(() =>
                cancelled.SetItemPrice(cancelled.Items.Single().Id, Money.From(500m)))
            .Code.ShouldBe("Appointment.PriceCanOnlyBeSetWhilePending");
    }

    // ---- The tattoo flow (RN19) ----

    [Fact]
    public void A_service_on_request_should_only_be_confirmed_after_its_price_is_set()
    {
        var appointment = Create(new[] { OnRequestItem(hours: 3) }, requiresConfirmation: false);
        appointment.Status.ShouldBe(AppointmentStatus.Pending);

        Should.Throw<DomainException>(() => appointment.Confirm(StaffId, Now))
            .Code.ShouldBe("Appointment.PriceStillPending");

        appointment.SetItemPrice(appointment.Items.Single().Id, Money.From(500m), TimeSpan.FromHours(4));
        appointment.Confirm(StaffId, Now);

        appointment.Status.ShouldBe(AppointmentStatus.Confirmed);
        appointment.TotalPrice.ShouldBe(Money.From(500m));
        appointment.DomainEvents.OfType<AppointmentConfirmed>().Single().TotalPrice.ShouldBe(Money.From(500m));
    }
}