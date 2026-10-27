using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.SharedKernel;
using PlanelyApp.Infrastructure.Persistence;
using PlanelyApp.IntegrationTests.Catalog;
using PlanelyApp.IntegrationTests.Organization;
using PlanelyApp.IntegrationTests.Scheduling;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Persistence;

/// <summary>
/// The Domain never lets an invalid value through, so these tests save valid data and then break it with SQL
/// straight into the table, the way a bug or a manual fix could. The CHECK must refuse it.
/// </summary>
public class CheckConstraintTests(MySqlFixture mysql)
{
    private static readonly string[] Expected =
    [
        "ck_branch_schedule_range",
        "ck_branch_schedule_break_range",
        "ck_working_hours_range",
        "ck_working_hours_break_range",
        "ck_time_off_period",
        "ck_appointment_period",
        "ck_promotion_validity",
        "ck_promotion_discount_positive",
        "ck_promotion_percentage_up_to_100",
        "ck_service_offering_pricing",
    ];

    private static readonly ValidityPeriod November =
        ValidityPeriod.Create(new DateOnly(2027, 11, 1), new DateOnly(2027, 11, 30));

    [Fact]
    public async Task The_constraints_should_exist_and_be_enforced()
    {
        await using var scope = mysql.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PlanelyDbContext>();

        var actual = await context.Database
            .SqlQuery<string>($"SELECT constraint_name AS Value FROM information_schema.table_constraints WHERE table_schema = DATABASE() AND constraint_type = 'CHECK' AND enforced = 'YES'")
            .ToListAsync(TestContext.Current.CancellationToken);

        Expected.ShouldBeSubsetOf(actual);
    }

    [Fact]
    public async Task Time_off_that_ends_before_it_starts_should_be_refused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = (await SeedProfessionalAsync(cancellationToken)).ToString();

        await AssertRefusedAsync(
            "ck_time_off_period",
            $"UPDATE time_off SET ends_at = starts_at WHERE professional_id = {id}",
            cancellationToken);
    }

    [Fact]
    public async Task Working_hours_and_their_breaks_with_an_empty_range_should_be_refused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = (await SeedProfessionalAsync(cancellationToken)).ToString();

        await AssertRefusedAsync(
            "ck_working_hours_range",
            $"UPDATE working_hours SET end_time = start_time WHERE professional_id = {id}",
            cancellationToken);

        await AssertRefusedAsync(
            "ck_working_hours_break_range",
            $"UPDATE working_hours_break SET end_time = start_time WHERE working_hours_id IN (SELECT id FROM working_hours WHERE professional_id = {id})",
            cancellationToken);
    }

    [Fact]
    public async Task Business_hours_and_their_breaks_with_an_empty_range_should_be_refused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var branch = OrganizationData.NewBranch(company.Id, CatalogData.NewName("Branch"));
        branch.SetBusinessHours(OrganizationData.WorkDay(DayOfWeek.Monday));

        await mysql.SaveAsync(company.Id, cancellationToken,
            services => services.GetRequiredService<IBranchRepository>().Add(branch));

        var id = branch.Id.ToString();

        await AssertRefusedAsync(
            "ck_branch_schedule_range",
            $"UPDATE branch_schedule SET end_time = start_time WHERE branch_id = {id}",
            cancellationToken);

        await AssertRefusedAsync(
            "ck_branch_schedule_break_range",
            $"UPDATE branch_schedule_break SET end_time = start_time WHERE branch_schedule_id IN (SELECT id FROM branch_schedule WHERE branch_id = {id})",
            cancellationToken);
    }

    [Fact]
    public async Task An_appointment_that_ends_before_it_starts_should_be_refused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SchedulingData.SeedAsync(mysql, cancellationToken);
        var service = CatalogData.NewFixedService(scenario.Company.Id);
        var appointment = SchedulingData.NewAppointment(
            scenario, SchedulingData.StartIn(3), [SchedulingData.FixedItem(service)]);

        await mysql.SaveAsync(scenario.Company.Id, cancellationToken,
            services => services.GetRequiredService<IAppointmentRepository>().Add(appointment));

        await AssertRefusedAsync(
            "ck_appointment_period",
            $"UPDATE appointment SET ends_at = starts_at WHERE id = {appointment.Id.ToString()}",
            cancellationToken);
    }

    [Fact]
    public async Task A_promotion_that_ends_before_it_starts_should_be_refused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = (await SeedPromotionAsync(Discount.Percentage(10m), November, cancellationToken)).ToString();

        await AssertRefusedAsync(
            "ck_promotion_validity",
            $"UPDATE promotion SET valid_until = DATE_SUB(valid_from, INTERVAL 1 DAY) WHERE id = {id}",
            cancellationToken);
    }

    [Fact]
    public async Task A_promotion_discount_that_is_zero_or_a_percentage_above_100_should_be_refused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = (await SeedPromotionAsync(Discount.Percentage(10m), November, cancellationToken)).ToString();

        await AssertRefusedAsync(
            "ck_promotion_discount_positive",
            $"UPDATE promotion SET discount_value = 0 WHERE id = {id}",
            cancellationToken);

        await AssertRefusedAsync(
            "ck_promotion_percentage_up_to_100",
            $"UPDATE promotion SET discount_value = 100.01 WHERE id = {id}",
            cancellationToken);
    }

    [Fact]
    public async Task Promotions_at_the_limits_the_domain_allows_should_be_accepted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var oneDay = ValidityPeriod.Create(new DateOnly(2027, 11, 10), new DateOnly(2027, 11, 10));

        // Both ends inclusive; 100% is allowed; a fixed amount may be above 100 (it is money, not a percentage).
        await SeedPromotionAsync(Discount.Percentage(100m), oneDay, cancellationToken);
        await SeedPromotionAsync(Discount.FixedAmount(Money.From(150m)), November, cancellationToken);
    }

    [Fact]
    public async Task A_fixed_price_service_without_price_should_be_refused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var service = CatalogData.NewFixedService(company.Id);

        await mysql.SaveAsync(company.Id, cancellationToken,
            services => services.GetRequiredService<IServiceOfferingRepository>().Add(service));

        await AssertRefusedAsync(
            "ck_service_offering_pricing",
            $"UPDATE service_offering SET base_price = NULL WHERE id = {service.Id.ToString()}",
            cancellationToken);
    }

    [Fact]
    public async Task An_on_request_service_with_a_price_should_be_refused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var service = CatalogData.NewOnRequestService(company.Id);

        await mysql.SaveAsync(company.Id, cancellationToken,
            services => services.GetRequiredService<IServiceOfferingRepository>().Add(service));

        await AssertRefusedAsync(
            "ck_service_offering_pricing",
            $"UPDATE service_offering SET base_price = 10 WHERE id = {service.Id.ToString()}",
            cancellationToken);
    }

    private async Task<Guid> SeedProfessionalAsync(CancellationToken cancellationToken)
    {
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var branch = await StaffData.SeedBranchAsync(mysql, company.Id, cancellationToken);
        var professional = Professional.Create(company.Id, "Ana Souza");

        professional.SetWorkingHours(branch.Id, OrganizationData.WorkDay(DayOfWeek.Monday));
        professional.AddTimeOff(DateTimeRange.Create(SchedulingData.StartIn(5), SchedulingData.StartIn(6)), "Trip");

        await mysql.SaveAsync(company.Id, cancellationToken,
            services => services.GetRequiredService<IProfessionalRepository>().Add(professional));

        return professional.Id;
    }

    private async Task<Guid> SeedPromotionAsync(Discount discount, ValidityPeriod validity, CancellationToken cancellationToken)
    {
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var service = CatalogData.NewFixedService(company.Id);
        var promotion = Promotion.CreateForAllBranches(company.Id, "Offer", discount, validity, [service], "Test");

        await mysql.SaveAsync(company.Id, cancellationToken,
            services => services.GetRequiredService<IPromotionRepository>().Add(promotion));

        return promotion.Id;
    }

    private async Task AssertRefusedAsync(string constraint, FormattableString sql, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PlanelyDbContext>();

        var exception = await Record.ExceptionAsync(
            () => context.Database.ExecuteSqlInterpolatedAsync(sql, cancellationToken));

        exception.ShouldNotBeNull();
        exception.Message.ShouldContain(constraint);
    }
}