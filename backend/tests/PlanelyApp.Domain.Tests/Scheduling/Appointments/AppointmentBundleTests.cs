using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.Scheduling.Pricing;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;
using static PlanelyApp.Domain.Tests.Scheduling.Appointments.AppointmentFixtures;

namespace PlanelyApp.Domain.Tests.Scheduling.Appointments;

public class AppointmentBundleTests
{
    private static NewAppointmentItem BundleItem(Guid promotionId, decimal price, decimal share, int minutes = 45) =>
        new(
            ServiceOffering.CreateWithFixedPrice(CompanyId, "Service", TimeSpan.FromMinutes(minutes), Money.From(price)),
            TimeSpan.FromMinutes(minutes),
            PriceQuote.FromBundle(Money.From(price), Money.From(share), promotionId));

    private static NewAppointmentItem SimplePromotionItem(Guid promotionId, decimal price, decimal discount) =>
        new(
            ServiceOffering.CreateWithFixedPrice(CompanyId, "Service", TimeSpan.FromMinutes(45), Money.From(price)),
            TimeSpan.FromMinutes(45),
            PriceQuote.Fixed(Money.From(price), Money.From(discount), promotionId));

    // ---- Creation ----

    [Fact]
    public void Create_should_keep_the_share_of_the_bundle_discount_in_each_item()
    {
        var bundleId = Guid.CreateVersion7();

        var appointment = Create([BundleItem(bundleId, 100m, 10m), BundleItem(bundleId, 50m, 5m)]);

        appointment.Items.ShouldAllBe(item => item.IsFromBundle && item.PromotionId == bundleId);
        appointment.Items[0].Discount.ShouldBe(Money.From(10m));
        appointment.Items[1].FinalPrice.ShouldBe(Money.From(45m));
        appointment.TotalPrice.ShouldBe(Money.From(135m));
    }

    [Fact]
    public void Create_should_accept_more_than_one_complete_bundle()
    {
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();

        var appointment = Create([
            BundleItem(first, 100m, 10m), BundleItem(first, 50m, 5m),
            BundleItem(second, 100m, 20m), BundleItem(second, 100m, 20m)]);

        appointment.Items.Count.ShouldBe(4);
    }

    [Fact]
    public void Create_should_reject_a_bundle_with_a_single_item()
    {
        Should.Throw<DomainException>(() => Create([BundleItem(Guid.CreateVersion7(), 100m, 10m)]))
            .Code.ShouldBe("Appointment.BundleIncomplete");
    }

    [Fact]
    public void Create_should_reject_a_bundle_that_is_complete_only_because_of_other_bundles()
    {
        var first = Guid.CreateVersion7();

        Should.Throw<DomainException>(() => Create([
                BundleItem(first, 100m, 10m),
                BundleItem(Guid.CreateVersion7(), 100m, 10m)]))
            .Code.ShouldBe("Appointment.BundleIncomplete");
    }

    [Fact]
    public void An_item_with_a_simple_promotion_should_not_be_treated_as_a_bundle()
    {
        var appointment = Create([SimplePromotionItem(Guid.CreateVersion7(), 100m, 10m)]);

        appointment.Items[0].IsFromBundle.ShouldBeFalse();
    }

    // ---- Adding ----

    [Fact]
    public void AddItemByStaff_should_reject_an_item_priced_as_part_of_a_bundle()
    {
        var appointment = Create();

        Should.Throw<DomainException>(() => appointment.AddItemByStaff(StaffId, BundleItem(Guid.CreateVersion7(), 50m, 5m)))
            .Code.ShouldBe("Appointment.BundleItemCannotBeAdded");

        appointment.Items.Count.ShouldBe(1);
        appointment.TotalPrice.ShouldBe(Money.From(80m));
    }

    [Fact]
    public void AddItemByCustomer_should_reject_an_item_priced_as_part_of_a_bundle()
    {
        var appointment = Create();

        Should.Throw<DomainException>(() => appointment.AddItemByCustomer(
                CustomerId, BundleItem(Guid.CreateVersion7(), 50m, 5m), Policy(), Now))
            .Code.ShouldBe("Appointment.BundleItemCannotBeAdded");
    }

    // ---- Removing ----

    [Fact]
    public void Removing_a_bundle_item_should_take_the_discount_off_the_other_item()
    {
        var bundleId = Guid.CreateVersion7();
        var appointment = Create([BundleItem(bundleId, 100m, 10m), BundleItem(bundleId, 50m, 5m), FixedItem(price: 40m)]);

        appointment.RemoveItemByStaff(StaffId, appointment.Items[0].Id);

        var remaining = appointment.Items[0];
        remaining.IsFromBundle.ShouldBeFalse();
        remaining.PromotionId.ShouldBeNull();
        remaining.Discount.ShouldBe(Money.Zero);
        remaining.ListPrice.ShouldBe(Money.From(50m));
        remaining.FinalPrice.ShouldBe(Money.From(50m));
        appointment.TotalPrice.ShouldBe(Money.From(90m));
    }

    [Fact]
    public void Removing_one_item_of_a_bundle_of_three_should_take_the_discount_off_the_other_two()
    {
        var bundleId = Guid.CreateVersion7();
        var appointment = Create([
            BundleItem(bundleId, 100m, 10m), BundleItem(bundleId, 100m, 10m), BundleItem(bundleId, 100m, 10m)]);

        appointment.RemoveItemByStaff(StaffId, appointment.Items[2].Id);

        appointment.Items.ShouldAllBe(item => !item.IsFromBundle && item.Discount == Money.Zero);
        appointment.TotalPrice.ShouldBe(Money.From(200m));
    }

    [Fact]
    public void Removing_by_the_customer_should_also_take_the_discount_off()
    {
        var bundleId = Guid.CreateVersion7();
        var appointment = Create(
            [BundleItem(bundleId, 100m, 10m), BundleItem(bundleId, 50m, 5m)], requiresConfirmation: false);

        appointment.RemoveItemByCustomer(CustomerId, appointment.Items[1].Id, Policy(), Now);

        appointment.Items.Single().FinalPrice.ShouldBe(Money.From(100m));
        appointment.TotalPrice.ShouldBe(Money.From(100m));
    }

    [Fact]
    public void Removing_a_bundle_item_should_record_the_event_with_the_total_without_the_discount()
    {
        var bundleId = Guid.CreateVersion7();
        var appointment = Create([BundleItem(bundleId, 100m, 10m), BundleItem(bundleId, 50m, 5m)]);

        appointment.RemoveItemByStaff(StaffId, appointment.Items[0].Id);

        appointment.DomainEvents.OfType<AppointmentServicesChanged>().Single()
            .TotalPrice.ShouldBe(Money.From(50m));
    }

    [Fact]
    public void Removing_an_item_outside_the_bundle_should_keep_the_bundle_discounts()
    {
        var bundleId = Guid.CreateVersion7();
        var appointment = Create([BundleItem(bundleId, 100m, 10m), BundleItem(bundleId, 50m, 5m), FixedItem(price: 40m)]);

        appointment.RemoveItemByStaff(StaffId, appointment.Items[2].Id);

        appointment.Items.ShouldAllBe(item => item.IsFromBundle);
        appointment.TotalPrice.ShouldBe(Money.From(135m));
    }

    [Fact]
    public void Removing_an_item_with_a_simple_promotion_should_not_touch_the_others()
    {
        var bundleId = Guid.CreateVersion7();
        var appointment = Create([
            BundleItem(bundleId, 100m, 10m), BundleItem(bundleId, 50m, 5m),
            SimplePromotionItem(Guid.CreateVersion7(), 100m, 20m)]);

        appointment.RemoveItemByStaff(StaffId, appointment.Items[2].Id);

        appointment.Items.ShouldAllBe(item => item.IsFromBundle);
        appointment.TotalPrice.ShouldBe(Money.From(135m));
    }

    [Fact]
    public void Removing_an_unknown_item_should_leave_the_bundle_untouched()
    {
        var bundleId = Guid.CreateVersion7();
        var appointment = Create([BundleItem(bundleId, 100m, 10m), BundleItem(bundleId, 50m, 5m)]);

        Should.Throw<DomainException>(() => appointment.RemoveItemByStaff(StaffId, Guid.CreateVersion7()))
            .Code.ShouldBe("Appointment.ItemNotFound");

        appointment.Items.ShouldAllBe(item => item.IsFromBundle);
        appointment.TotalPrice.ShouldBe(Money.From(135m));
    }
}