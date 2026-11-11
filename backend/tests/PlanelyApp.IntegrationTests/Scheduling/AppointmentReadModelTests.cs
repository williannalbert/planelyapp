using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Application.Scheduling.Appointments;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.IntegrationTests.Catalog;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Scheduling;

/// <summary>
/// The read model against MySQL. Ana and Bruno, two branches (Centro and Norte) and two customers (João and another):
///   a  Centro  João   Ana    day 5 at 10:00   confirmed
///   b  Centro  João   Bruno  day 5 at 15:00   confirmed
///   c  Norte   other  Ana    day 6 at 10:00   confirmed
///   d  Centro  João   Ana    day 7 at 10:00   cancelled
///   e  Centro  other  Bruno  day 8 at 10:00   pending (service on request, no price)
/// </summary>
public class AppointmentReadModelTests(MySqlFixture mysql)
{
    [Fact]
    public async Task The_branch_filter_should_list_its_appointments_by_start()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var data = await SeedAsync(cancellationToken);

        var page = await ListAsync(data, new AppointmentFilter(BranchId: data.Scenario.Branch.Id), cancellationToken);

        page.Items.Select(item => item.Id).ToList().ShouldBe([data.A, data.B, data.D, data.E], ignoreOrder: false);
        page.TotalCount.ShouldBe(4);
    }

    [Fact]
    public async Task The_window_should_include_the_start_and_leave_out_the_end()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var data = await SeedAsync(cancellationToken);

        // a starts exactly at the beginning of the window and b exactly at its end.
        var page = await ListAsync(
            data,
            new AppointmentFilter(FromUtc: SchedulingData.StartIn(5, 10), ToUtc: SchedulingData.StartIn(5, 15)),
            cancellationToken);

        page.Items.Select(item => item.Id).ToList().ShouldBe([data.A], ignoreOrder: false);
    }

    [Fact]
    public async Task The_professional_filter_should_list_only_that_professional()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var data = await SeedAsync(cancellationToken);

        var page = await ListAsync(data, new AppointmentFilter(ProfessionalId: data.Bruno.Id), cancellationToken);

        page.Items.Select(item => item.Id).ToList().ShouldBe([data.B, data.E], ignoreOrder: false);
    }

    [Fact]
    public async Task The_status_filter_should_list_only_that_status()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var data = await SeedAsync(cancellationToken);

        var confirmed = await ListAsync(
            data, new AppointmentFilter(Status: AppointmentStatus.Confirmed), cancellationToken);
        var cancelled = await ListAsync(
            data, new AppointmentFilter(Status: AppointmentStatus.Cancelled), cancellationToken);
        var pending = await ListAsync(
            data, new AppointmentFilter(Status: AppointmentStatus.Pending), cancellationToken);

        confirmed.Items.Select(item => item.Id).ToList().ShouldBe([data.A, data.B, data.C], ignoreOrder: false);
        cancelled.Items.Select(item => item.Id).ToList().ShouldBe([data.D], ignoreOrder: false);
        pending.Items.Select(item => item.Id).ToList().ShouldBe([data.E], ignoreOrder: false);
    }

    [Fact]
    public async Task The_allowed_branches_should_limit_the_list()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var data = await SeedAsync(cancellationToken);

        var onlyNorte = await ListAsync(
            data, new AppointmentFilter(AllowedBranchIds: [data.Norte.Id]), cancellationToken);
        var none = await ListAsync(data, new AppointmentFilter(AllowedBranchIds: []), cancellationToken);

        onlyNorte.Items.Select(item => item.Id).ToList().ShouldBe([data.C], ignoreOrder: false);
        none.TotalCount.ShouldBe(0);
        none.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_history_of_a_customer_should_come_newest_first()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var data = await SeedAsync(cancellationToken);

        var page = await ListAsync(
            data,
            new AppointmentFilter(CustomerId: data.Scenario.Customer.Id, NewestFirst: true),
            cancellationToken);

        page.Items.Select(item => item.Id).ToList().ShouldBe([data.D, data.B, data.A], ignoreOrder: false);
    }

    [Fact]
    public async Task The_upcoming_should_leave_out_the_past_and_what_no_longer_takes_up_the_agenda()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var data = await SeedAsync(cancellationToken);

        // From day 5 at noon: a is in the past and d is cancelled.
        var page = await ListAsync(
            data, new AppointmentFilter(UpcomingFromUtc: SchedulingData.StartIn(5, 12)), cancellationToken);

        page.Items.Select(item => item.Id).ToList().ShouldBe([data.B, data.C, data.E], ignoreOrder: false);
    }

    [Fact]
    public async Task The_items_should_carry_names_period_services_and_price()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var data = await SeedAsync(cancellationToken);

        var page = await ListAsync(data, new AppointmentFilter(), cancellationToken);

        var a = page.Items.Single(item => item.Id == data.A);
        a.BranchId.ShouldBe(data.Scenario.Branch.Id);
        a.BranchName.ShouldBe(data.Scenario.Branch.Name);
        a.CustomerName.ShouldBe(data.Scenario.Customer.Name);
        a.ProfessionalName.ShouldBe(data.Scenario.Professional.Name);
        a.StartUtc.ShouldBe(SchedulingData.StartIn(5, 10));
        a.EndUtc.ShouldBe(SchedulingData.StartIn(5, 10).Add(data.Haircut.StandardDuration));
        a.Status.ShouldBe(AppointmentStatus.Confirmed);
        a.TotalPrice.ShouldBe(data.Haircut.BasePrice!.Amount);
        a.HasPendingPrice.ShouldBeFalse();
        a.Services.ShouldBe([data.Haircut.Name], ignoreOrder: false);

        var e = page.Items.Single(item => item.Id == data.E);
        e.Status.ShouldBe(AppointmentStatus.Pending);
        e.TotalPrice.ShouldBeNull();
        e.HasPendingPrice.ShouldBeTrue();
        e.Services.ShouldBe([data.Tattoo.Name], ignoreOrder: false);
    }

    [Fact]
    public async Task A_page_should_have_its_part_and_the_size_of_the_whole_list()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var data = await SeedAsync(cancellationToken);

        var page = await ListAsync(
            data, new AppointmentFilter(BranchId: data.Scenario.Branch.Id), cancellationToken, page: 2, pageSize: 2);

        page.Items.Select(item => item.Id).ToList().ShouldBe([data.D, data.E], ignoreOrder: false);
        page.Page.ShouldBe(2);
        page.TotalCount.ShouldBe(4);
        page.TotalPages.ShouldBe(2);
    }

    [Fact]
    public async Task Another_company_should_never_appear()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var data = await SeedAsync(cancellationToken);
        var other = await SeedAsync(cancellationToken);

        var page = await ListAsync(data, new AppointmentFilter(), cancellationToken);

        page.TotalCount.ShouldBe(5);
        page.Items.Select(item => item.Id).ShouldNotContain(other.A);
    }

    // ---- Helpers ----

    private sealed record Seeded(
        Scenario Scenario,
        Branch Norte,
        Professional Bruno,
        Customer Other,
        Guid A,
        Guid B,
        Guid C,
        Guid D,
        Guid E,
        PlanelyApp.Domain.Catalog.ServiceOfferings.ServiceOffering Haircut,
        PlanelyApp.Domain.Catalog.ServiceOfferings.ServiceOffering Tattoo);

    private async Task<Seeded> SeedAsync(CancellationToken cancellationToken)
    {
        var scenario = await SchedulingData.SeedAsync(mysql, cancellationToken);
        var companyId = scenario.Company.Id;
        var norte = await StaffData.SeedBranchAsync(mysql, companyId, cancellationToken);
        var bruno = await SchedulingData.SeedProfessionalAsync(mysql, companyId, cancellationToken);
        var other = StaffData.NewCustomer(companyId);
        await mysql.SaveAsync(companyId, cancellationToken,
            services => services.GetRequiredService<ICustomerRepository>().Add(other));

        var haircut = CatalogData.NewFixedService(companyId);
        var tattoo = CatalogData.NewOnRequestService(companyId);
        var centro = scenario.Branch.Id;
        var joao = scenario.Customer.Id;
        var ana = scenario.Professional.Id;

        var a = New(companyId, centro, joao, ana, SchedulingData.StartIn(5, 10), SchedulingData.FixedItem(haircut));
        var b = New(companyId, centro, joao, bruno.Id, SchedulingData.StartIn(5, 15), SchedulingData.FixedItem(haircut));
        var c = New(companyId, norte.Id, other.Id, ana, SchedulingData.StartIn(6, 10), SchedulingData.FixedItem(haircut));
        var d = New(companyId, centro, joao, ana, SchedulingData.StartIn(7, 10), SchedulingData.FixedItem(haircut));
        d.CancelByStaff(SchedulingData.Actor, SchedulingData.Now);
        var e = New(companyId, centro, other.Id, bruno.Id, SchedulingData.StartIn(8, 10), SchedulingData.OnRequestItem(tattoo));

        await mysql.SaveAsync(companyId, cancellationToken, services =>
        {
            var repository = services.GetRequiredService<IAppointmentRepository>();

            foreach (var appointment in new[] { a, b, c, d, e })
            {
                repository.Add(appointment);
            }
        });

        return new Seeded(scenario, norte, bruno, other, a.Id, b.Id, c.Id, d.Id, e.Id, haircut, tattoo);
    }

    private static Appointment New(
        Guid companyId,
        Guid branchId,
        Guid customerId,
        Guid professionalId,
        DateTime start,
        NewAppointmentItem item) =>
        Appointment.Create(
            companyId,
            branchId,
            customerId,
            professionalId,
            start,
            [item],
            BookingPolicy.Default,
            SchedulingData.Now,
            SchedulingData.Actor);

    private async Task<PlanelyApp.Application.Abstractions.Paging.PagedResult<AppointmentSummaryDto>> ListAsync(
        Seeded data,
        AppointmentFilter filter,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 50)
    {
        await using var scope = mysql.CreateScope(data.Scenario.Company.Id);

        return await scope.ServiceProvider.GetRequiredService<IAppointmentReadModel>()
            .ListAsync(filter, page, pageSize, cancellationToken);
    }
}