using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Catalog.Promotions;

public class PromotionTests
{
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly ValidityPeriod November =
        ValidityPeriod.Create(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));

    // 2026-11-06 is a Friday and 2026-11-09 is a Monday.
    private static readonly DateOnly Friday = new(2026, 11, 6);
    private static readonly DateOnly Monday = new(2026, 11, 9);

    private static ServiceOffering FixedService(Guid? companyId = null, string name = "Haircut") =>
        ServiceOffering.CreateWithFixedPrice(
            companyId ?? CompanyId, name, TimeSpan.FromMinutes(45), Money.From(80m));

    private static ServiceOffering OnRequestService() =>
        ServiceOffering.CreateOnRequest(CompanyId, "Custom tattoo", TimeSpan.FromHours(3));

    private static Promotion CreateForAllBranches(ServiceOffering service) =>
        Promotion.CreateForAllBranches(
            CompanyId, "Black Friday", Discount.Percentage(10m), November, [service]);

    private static Promotion CreateForBranches(ServiceOffering service, params Guid[] branchIds) =>
        Promotion.CreateForBranches(
            CompanyId, "Black Friday", Discount.Percentage(10m), November, [service], branchIds);

    // ---- Creation ----

    [Fact]
    public void CreateForAllBranches_should_start_active_and_valid()
    {
        var service = FixedService();

        var promotion = Promotion.CreateForAllBranches(
            CompanyId, "  Black Friday  ", Discount.Percentage(10m), November, [service], "  30 days  ");

        promotion.CompanyId.ShouldBe(CompanyId);
        promotion.Name.ShouldBe("Black Friday");
        promotion.Description.ShouldBe("30 days");
        promotion.Discount.ShouldBe(Discount.Percentage(10m));
        promotion.Validity.ShouldBe(November);
        promotion.AppliesToAllBranches.ShouldBeTrue();
        promotion.BranchIds.ShouldBeEmpty();
        promotion.ServiceIds.ShouldBe([service.Id]);
        promotion.Weekdays.ShouldBeEmpty();
        promotion.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void CreateForBranches_should_keep_the_branches_without_duplicates()
    {
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();

        var promotion = CreateForBranches(FixedService(), first, second, first);

        promotion.AppliesToAllBranches.ShouldBeFalse();
        promotion.BranchIds.ShouldBe([first, second]);
    }

    [Fact]
    public void Create_should_keep_each_service_once()
    {
        var service = FixedService();
        var other = FixedService(name: "Beard");

        var promotion = Promotion.CreateForAllBranches(
            CompanyId, "Black Friday", Discount.Percentage(10m), November, [service, other, service]);

        promotion.ServiceIds.ShouldBe([service.Id, other.Id]);
    }

    [Fact]
    public void Create_should_record_the_PromotionCreated_event()
    {
        var branchId = Guid.CreateVersion7();

        var promotion = CreateForBranches(FixedService(), branchId);

        var created = promotion.DomainEvents.OfType<PromotionCreated>().Single();
        created.PromotionId.ShouldBe(promotion.Id);
        created.CompanyId.ShouldBe(CompanyId);
        created.AppliesToAllBranches.ShouldBeFalse();
        created.BranchIds.ShouldBe([branchId]);
    }

    [Fact]
    public void The_PromotionCreated_event_should_not_follow_later_changes()
    {
        var branchId = Guid.CreateVersion7();
        var promotion = CreateForBranches(FixedService(), branchId);

        promotion.ApplyToAllBranches();

        var created = promotion.DomainEvents.OfType<PromotionCreated>().Single();
        created.BranchIds.ShouldBe([branchId]);
        created.AppliesToAllBranches.ShouldBeFalse();
    }

    [Fact]
    public void Create_without_company_should_be_rejected()
    {
        Should.Throw<DomainException>(() => Promotion.CreateForAllBranches(
                Guid.Empty, "Black Friday", Discount.Percentage(10m), November, [FixedService()]))
            .Code.ShouldBe("Promotion.CompanyRequired");
    }

    [Fact]
    public void Create_with_an_invalid_name_should_be_rejected()
    {
        foreach (var name in new[] { null, "", "   ", new string('a', 101) })
        {
            Should.Throw<DomainException>(() => Promotion.CreateForAllBranches(
                    CompanyId, name, Discount.Percentage(10m), November, [FixedService()]))
                .Code.ShouldBe("Promotion.NameInvalid");
        }
    }

    [Fact]
    public void Create_with_a_description_that_is_too_long_should_be_rejected()
    {
        Should.Throw<DomainException>(() => Promotion.CreateForAllBranches(
                CompanyId, "Black Friday", Discount.Percentage(10m), November, [FixedService()], new string('a', 1001)))
            .Code.ShouldBe("Promotion.DescriptionInvalid");
    }

    [Fact]
    public void Create_without_services_should_be_rejected_RN17()
    {
        Should.Throw<DomainException>(() => Promotion.CreateForAllBranches(
                CompanyId, "Black Friday", Discount.Percentage(10m), November, []))
            .Code.ShouldBe("Promotion.ServicesRequired");
    }

    [Fact]
    public void Create_with_a_service_of_another_company_should_be_rejected()
    {
        var foreign = FixedService(companyId: Guid.CreateVersion7());

        Should.Throw<DomainException>(() => Promotion.CreateForAllBranches(
                CompanyId, "Black Friday", Discount.Percentage(10m), November, [foreign]))
            .Code.ShouldBe("Promotion.ServiceFromAnotherCompany");
    }

    [Fact]
    public void Create_with_an_on_request_service_should_be_rejected_RN20()
    {
        Should.Throw<DomainException>(() => Promotion.CreateForAllBranches(
                CompanyId, "Black Friday", Discount.Percentage(10m), November, [OnRequestService()]))
            .Code.ShouldBe("Promotion.OnRequestServiceNotAllowed");
    }

    [Fact]
    public void CreateForBranches_without_branches_should_be_rejected_RN17()
    {
        Should.Throw<DomainException>(() => CreateForBranches(FixedService()))
            .Code.ShouldBe("Promotion.BranchesRequired");
    }

    [Fact]
    public void CreateForBranches_with_an_empty_branch_id_should_be_rejected()
    {
        Should.Throw<DomainException>(() => CreateForBranches(FixedService(), Guid.CreateVersion7(), Guid.Empty))
            .Code.ShouldBe("Promotion.BranchInvalid");
    }

    // ---- Details, discount and validity ----

    [Fact]
    public void UpdateDetails_should_change_name_and_description()
    {
        var promotion = CreateForAllBranches(FixedService());

        promotion.UpdateDetails("Cyber Monday", "  Only online  ");

        promotion.Name.ShouldBe("Cyber Monday");
        promotion.Description.ShouldBe("Only online");
    }

    [Fact]
    public void UpdateDetails_with_an_invalid_value_should_leave_the_promotion_untouched()
    {
        var promotion = CreateForAllBranches(FixedService());

        Should.Throw<DomainException>(() => promotion.UpdateDetails("Cyber Monday", new string('a', 1001)))
            .Code.ShouldBe("Promotion.DescriptionInvalid");

        promotion.Name.ShouldBe("Black Friday");
    }

    [Fact]
    public void ChangeDiscount_and_ChangeValidity_should_replace_the_values()
    {
        var promotion = CreateForAllBranches(FixedService());
        var december = ValidityPeriod.Create(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31));

        promotion.ChangeDiscount(Discount.FixedAmount(Money.From(15m)));
        promotion.ChangeValidity(december);

        promotion.Discount.ShouldBe(Discount.FixedAmount(Money.From(15m)));
        promotion.Validity.ShouldBe(december);
    }

    // ---- Weekdays ----

    [Fact]
    public void SetWeekdays_should_keep_them_ordered_and_without_duplicates()
    {
        var promotion = CreateForAllBranches(FixedService());

        promotion.SetWeekdays([DayOfWeek.Saturday, DayOfWeek.Monday, DayOfWeek.Saturday]);

        promotion.Weekdays.ShouldBe([DayOfWeek.Monday, DayOfWeek.Saturday]);
    }

    [Fact]
    public void SetWeekdays_with_an_empty_list_should_mean_every_day()
    {
        var promotion = CreateForAllBranches(FixedService());
        promotion.SetWeekdays([DayOfWeek.Monday]);

        promotion.SetWeekdays([]);

        promotion.Weekdays.ShouldBeEmpty();
    }

    [Fact]
    public void SetWeekdays_with_an_invalid_day_should_leave_the_weekdays_untouched()
    {
        var promotion = CreateForAllBranches(FixedService());
        promotion.SetWeekdays([DayOfWeek.Monday]);

        Should.Throw<DomainException>(() => promotion.SetWeekdays([DayOfWeek.Friday, (DayOfWeek)9]))
            .Code.ShouldBe("Promotion.WeekdayInvalid");

        promotion.Weekdays.ShouldBe([DayOfWeek.Monday]);
    }

    // ---- Branches ----

    [Fact]
    public void ApplyToBranches_should_replace_the_scope()
    {
        var promotion = CreateForAllBranches(FixedService());
        var branchId = Guid.CreateVersion7();

        promotion.ApplyToBranches([branchId]);

        promotion.AppliesToAllBranches.ShouldBeFalse();
        promotion.BranchIds.ShouldBe([branchId]);
    }

    [Fact]
    public void ApplyToBranches_with_an_empty_list_should_leave_the_scope_untouched()
    {
        var branchId = Guid.CreateVersion7();
        var promotion = CreateForBranches(FixedService(), branchId);

        Should.Throw<DomainException>(() => promotion.ApplyToBranches([]))
            .Code.ShouldBe("Promotion.BranchesRequired");

        promotion.BranchIds.ShouldBe([branchId]);
    }

    [Fact]
    public void ApplyToAllBranches_should_clear_the_branch_list()
    {
        var promotion = CreateForBranches(FixedService(), Guid.CreateVersion7());

        promotion.ApplyToAllBranches();

        promotion.AppliesToAllBranches.ShouldBeTrue();
        promotion.BranchIds.ShouldBeEmpty();
    }

    // ---- Services ----

    [Fact]
    public void IncludeServices_should_add_new_services_and_ignore_repeated_ones()
    {
        var haircut = FixedService();
        var beard = FixedService(name: "Beard");
        var promotion = CreateForAllBranches(haircut);

        promotion.IncludeServices([haircut, beard]);

        promotion.ServiceIds.ShouldBe([haircut.Id, beard.Id]);
    }

    [Fact]
    public void IncludeServices_should_add_nothing_when_one_of_them_is_rejected()
    {
        var haircut = FixedService();
        var promotion = CreateForAllBranches(haircut);

        Should.Throw<DomainException>(() => promotion.IncludeServices([FixedService(name: "Beard"), OnRequestService()]))
            .Code.ShouldBe("Promotion.OnRequestServiceNotAllowed");

        promotion.ServiceIds.ShouldBe([haircut.Id]);
    }

    [Fact]
    public void IncludeServices_with_a_service_of_another_company_should_be_rejected()
    {
        var promotion = CreateForAllBranches(FixedService());

        Should.Throw<DomainException>(() => promotion.IncludeServices([FixedService(companyId: Guid.CreateVersion7())]))
            .Code.ShouldBe("Promotion.ServiceFromAnotherCompany");
    }

    [Fact]
    public void RemoveService_should_remove_it_while_others_remain()
    {
        var haircut = FixedService();
        var beard = FixedService(name: "Beard");
        var promotion = Promotion.CreateForAllBranches(
            CompanyId, "Black Friday", Discount.Percentage(10m), November, [haircut, beard]);

        promotion.RemoveService(haircut.Id);

        promotion.ServiceIds.ShouldBe([beard.Id]);
    }

    [Fact]
    public void RemoveService_of_a_service_that_is_not_there_should_do_nothing()
    {
        var haircut = FixedService();
        var promotion = CreateForAllBranches(haircut);

        promotion.RemoveService(Guid.CreateVersion7());

        promotion.ServiceIds.ShouldBe([haircut.Id]);
    }

    [Fact]
    public void RemoveService_should_not_remove_the_last_one_RN17()
    {
        var haircut = FixedService();
        var promotion = CreateForAllBranches(haircut);

        Should.Throw<DomainException>(() => promotion.RemoveService(haircut.Id))
            .Code.ShouldBe("Promotion.ServicesRequired");

        promotion.ServiceIds.ShouldBe([haircut.Id]);
    }

    // ---- AppliesTo ----

    [Fact]
    public void AppliesTo_should_be_true_for_a_listed_branch_and_service_inside_the_period()
    {
        var service = FixedService();
        var branchId = Guid.CreateVersion7();
        var promotion = CreateForBranches(service, branchId);

        promotion.AppliesTo(branchId, service.Id, Friday).ShouldBeTrue();
    }

    [Fact]
    public void AppliesTo_should_be_true_in_any_branch_when_it_applies_to_all()
    {
        var service = FixedService();
        var promotion = CreateForAllBranches(service);

        promotion.AppliesTo(Guid.CreateVersion7(), service.Id, Friday).ShouldBeTrue();
    }

    [Fact]
    public void AppliesTo_should_be_false_for_a_branch_that_is_not_listed()
    {
        var service = FixedService();
        var promotion = CreateForBranches(service, Guid.CreateVersion7());

        promotion.AppliesTo(Guid.CreateVersion7(), service.Id, Friday).ShouldBeFalse();
    }

    [Fact]
    public void AppliesTo_should_be_false_for_a_service_that_is_not_listed()
    {
        var promotion = CreateForAllBranches(FixedService());

        promotion.AppliesTo(Guid.CreateVersion7(), Guid.CreateVersion7(), Friday).ShouldBeFalse();
    }

    [Fact]
    public void AppliesTo_should_be_false_outside_the_validity_period()
    {
        var service = FixedService();
        var promotion = CreateForAllBranches(service);
        var branchId = Guid.CreateVersion7();

        promotion.AppliesTo(branchId, service.Id, new DateOnly(2026, 10, 31)).ShouldBeFalse();
        promotion.AppliesTo(branchId, service.Id, new DateOnly(2026, 11, 1)).ShouldBeTrue();
        promotion.AppliesTo(branchId, service.Id, new DateOnly(2026, 11, 30)).ShouldBeTrue();
        promotion.AppliesTo(branchId, service.Id, new DateOnly(2026, 12, 1)).ShouldBeFalse();
    }

    [Fact]
    public void AppliesTo_should_respect_the_weekdays_when_there_are_any()
    {
        var service = FixedService();
        var promotion = CreateForAllBranches(service);
        var branchId = Guid.CreateVersion7();
        promotion.SetWeekdays([DayOfWeek.Friday, DayOfWeek.Saturday]);

        promotion.AppliesTo(branchId, service.Id, Friday).ShouldBeTrue();
        promotion.AppliesTo(branchId, service.Id, Monday).ShouldBeFalse();
    }

    [Fact]
    public void AppliesTo_should_be_false_after_the_promotion_ends()
    {
        var service = FixedService();
        var promotion = CreateForAllBranches(service);

        promotion.End();

        promotion.AppliesTo(Guid.CreateVersion7(), service.Id, Friday).ShouldBeFalse();
    }

    // ---- Discount and ending ----

    [Fact]
    public void CalculateDiscount_should_use_the_discount_of_the_promotion()
    {
        var promotion = CreateForAllBranches(FixedService());

        promotion.CalculateDiscount(Money.From(80m)).ShouldBe(Money.From(8m));
    }

    [Fact]
    public void End_should_deactivate_the_promotion_and_be_repeatable()
    {
        var promotion = CreateForAllBranches(FixedService());

        promotion.End();
        promotion.End();

        promotion.IsActive.ShouldBeFalse();
    }
}