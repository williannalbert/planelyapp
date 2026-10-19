using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Catalog.Promotions;

public class PromotionBundleTests
{
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly ValidityPeriod November =
        ValidityPeriod.Create(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));

    // 2026-11-06 is a Friday and 2026-11-09 is a Monday.
    private static readonly DateOnly Friday = new(2026, 11, 6);
    private static readonly DateOnly Monday = new(2026, 11, 9);

    private static ServiceOffering FixedService(string name = "Haircut", Guid? companyId = null) =>
        ServiceOffering.CreateWithFixedPrice(
            companyId ?? CompanyId, name, TimeSpan.FromMinutes(45), Money.From(80m));

    private static Promotion CreateBundle(params ServiceOffering[] services) =>
        Promotion.CreateBundleForAllBranches(
            CompanyId, "Cut + beard", Discount.Percentage(10m), November, services);

    // ---- Creation ----

    [Fact]
    public void CreateBundleForAllBranches_should_create_a_bundle_with_all_its_services()
    {
        var haircut = FixedService("Haircut");
        var beard = FixedService("Beard");

        var bundle = CreateBundle(haircut, beard);

        bundle.Kind.ShouldBe(PromotionKind.Bundle);
        bundle.ServiceIds.ShouldBe([haircut.Id, beard.Id]);
        bundle.AppliesToAllBranches.ShouldBeTrue();
        bundle.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void CreateBundleForBranches_should_keep_the_branches()
    {
        var branchId = Guid.CreateVersion7();

        var bundle = Promotion.CreateBundleForBranches(
            CompanyId, "Cut + beard", Discount.Percentage(10m), November,
            [FixedService("Haircut"), FixedService("Beard")], [branchId]);

        bundle.Kind.ShouldBe(PromotionKind.Bundle);
        bundle.AppliesToAllBranches.ShouldBeFalse();
        bundle.BranchIds.ShouldBe([branchId]);
    }

    [Fact]
    public void The_per_service_factories_should_create_per_service_promotions()
    {
        var promotion = Promotion.CreateForAllBranches(
            CompanyId, "Promo", Discount.Percentage(10m), November, [FixedService()]);

        promotion.Kind.ShouldBe(PromotionKind.PerService);
    }

    [Fact]
    public void A_bundle_with_a_single_service_should_be_rejected()
    {
        Should.Throw<DomainException>(() => CreateBundle(FixedService()))
            .Code.ShouldBe("Promotion.BundleRequiresAtLeastTwoServices");
    }

    [Fact]
    public void A_bundle_with_the_same_service_twice_should_be_rejected()
    {
        var haircut = FixedService();

        Should.Throw<DomainException>(() => CreateBundle(haircut, haircut))
            .Code.ShouldBe("Promotion.BundleRequiresAtLeastTwoServices");
    }

    [Fact]
    public void A_bundle_without_services_should_be_rejected()
    {
        Should.Throw<DomainException>(() => CreateBundle())
            .Code.ShouldBe("Promotion.ServicesRequired");
    }

    [Fact]
    public void A_bundle_with_a_service_from_another_company_should_be_rejected()
    {
        var foreign = FixedService("Beard", companyId: Guid.CreateVersion7());

        Should.Throw<DomainException>(() => CreateBundle(FixedService("Haircut"), foreign))
            .Code.ShouldBe("Promotion.ServiceFromAnotherCompany");
    }

    [Fact]
    public void Creating_a_bundle_should_record_the_PromotionCreated_event()
    {
        var bundle = CreateBundle(FixedService("Haircut"), FixedService("Beard"));

        bundle.DomainEvents.OfType<PromotionCreated>().Single().PromotionId.ShouldBe(bundle.Id);
    }

    // ---- Services of a bundle ----

    [Fact]
    public void RemoveService_should_not_leave_a_bundle_with_fewer_than_two_services()
    {
        var haircut = FixedService("Haircut");
        var beard = FixedService("Beard");
        var bundle = CreateBundle(haircut, beard);

        Should.Throw<DomainException>(() => bundle.RemoveService(beard.Id))
            .Code.ShouldBe("Promotion.BundleRequiresAtLeastTwoServices");

        bundle.ServiceIds.ShouldBe([haircut.Id, beard.Id]);
    }

    [Fact]
    public void RemoveService_should_work_while_the_bundle_keeps_two_services()
    {
        var haircut = FixedService("Haircut");
        var beard = FixedService("Beard");
        var wash = FixedService("Wash");
        var bundle = CreateBundle(haircut, beard, wash);

        bundle.RemoveService(wash.Id);

        bundle.ServiceIds.ShouldBe([haircut.Id, beard.Id]);
    }

    [Fact]
    public void IncludeServices_should_make_the_new_service_required_in_the_bundle()
    {
        var haircut = FixedService("Haircut");
        var beard = FixedService("Beard");
        var wash = FixedService("Wash");
        var bundle = CreateBundle(haircut, beard);

        bundle.IncludeServices([wash]);

        bundle.AppliesToBundle(Guid.CreateVersion7(), [haircut.Id, beard.Id], Friday).ShouldBeFalse();
        bundle.AppliesToBundle(Guid.CreateVersion7(), [haircut.Id, beard.Id, wash.Id], Friday).ShouldBeTrue();
    }

    // ---- AppliesTo and AppliesToBundle ----

    [Fact]
    public void A_bundle_should_never_answer_AppliesTo()
    {
        var haircut = FixedService("Haircut");
        var bundle = CreateBundle(haircut, FixedService("Beard"));

        bundle.AppliesTo(Guid.CreateVersion7(), haircut.Id, Friday).ShouldBeFalse();
    }

    [Fact]
    public void A_per_service_promotion_should_never_answer_AppliesToBundle()
    {
        var haircut = FixedService("Haircut");
        var promotion = Promotion.CreateForAllBranches(
            CompanyId, "Promo", Discount.Percentage(10m), November, [haircut]);

        promotion.AppliesToBundle(Guid.CreateVersion7(), [haircut.Id], Friday).ShouldBeFalse();
    }

    [Fact]
    public void AppliesToBundle_should_be_true_when_all_the_services_are_present()
    {
        var haircut = FixedService("Haircut");
        var beard = FixedService("Beard");
        var bundle = CreateBundle(haircut, beard);

        bundle.AppliesToBundle(Guid.CreateVersion7(), [haircut.Id, beard.Id], Friday).ShouldBeTrue();
    }

    [Fact]
    public void AppliesToBundle_should_accept_extra_services_in_the_appointment()
    {
        var haircut = FixedService("Haircut");
        var beard = FixedService("Beard");
        var bundle = CreateBundle(haircut, beard);

        bundle.AppliesToBundle(Guid.CreateVersion7(), [Guid.CreateVersion7(), haircut.Id, beard.Id], Friday)
            .ShouldBeTrue();
    }

    [Fact]
    public void AppliesToBundle_should_be_false_when_a_service_is_missing()
    {
        var haircut = FixedService("Haircut");
        var bundle = CreateBundle(haircut, FixedService("Beard"));

        bundle.AppliesToBundle(Guid.CreateVersion7(), [haircut.Id], Friday).ShouldBeFalse();
        bundle.AppliesToBundle(Guid.CreateVersion7(), [], Friday).ShouldBeFalse();
    }

    [Fact]
    public void AppliesToBundle_should_be_false_for_an_ended_bundle()
    {
        var haircut = FixedService("Haircut");
        var beard = FixedService("Beard");
        var bundle = CreateBundle(haircut, beard);
        bundle.End();

        bundle.AppliesToBundle(Guid.CreateVersion7(), [haircut.Id, beard.Id], Friday).ShouldBeFalse();
    }

    [Fact]
    public void AppliesToBundle_should_be_false_outside_the_period()
    {
        var haircut = FixedService("Haircut");
        var beard = FixedService("Beard");
        var bundle = CreateBundle(haircut, beard);

        bundle.AppliesToBundle(Guid.CreateVersion7(), [haircut.Id, beard.Id], new DateOnly(2026, 12, 1))
            .ShouldBeFalse();
    }

    [Fact]
    public void AppliesToBundle_should_respect_the_weekdays()
    {
        var haircut = FixedService("Haircut");
        var beard = FixedService("Beard");
        var bundle = CreateBundle(haircut, beard);
        bundle.SetWeekdays([DayOfWeek.Monday]);

        bundle.AppliesToBundle(Guid.CreateVersion7(), [haircut.Id, beard.Id], Friday).ShouldBeFalse();
        bundle.AppliesToBundle(Guid.CreateVersion7(), [haircut.Id, beard.Id], Monday).ShouldBeTrue();
    }

    [Fact]
    public void AppliesToBundle_should_respect_the_branches()
    {
        var haircut = FixedService("Haircut");
        var beard = FixedService("Beard");
        var branchId = Guid.CreateVersion7();
        var bundle = Promotion.CreateBundleForBranches(
            CompanyId, "Cut + beard", Discount.Percentage(10m), November, [haircut, beard], [branchId]);

        bundle.AppliesToBundle(branchId, [haircut.Id, beard.Id], Friday).ShouldBeTrue();
        bundle.AppliesToBundle(Guid.CreateVersion7(), [haircut.Id, beard.Id], Friday).ShouldBeFalse();
    }
}