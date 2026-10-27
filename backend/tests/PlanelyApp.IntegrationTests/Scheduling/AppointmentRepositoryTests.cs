using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using PlanelyApp.IntegrationTests.Catalog;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Scheduling;

public class AppointmentRepositoryTests(MySqlFixture mysql)
{
    [Fact]
    public async Task A_confirmed_appointment_should_come_back_with_its_items_and_history()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SchedulingData.SeedAsync(mysql, cancellationToken);
        var haircut = CatalogData.NewFixedService(scenario.Company.Id, price: 100m, minutes: 45, name: "Corte masculino");
        var beard = CatalogData.NewFixedService(scenario.Company.Id, price: 40m, minutes: 30, name: "Barba");
        var promotionId = Guid.CreateVersion7();
        var start = SchedulingData.StartIn(3);

        var appointment = SchedulingData.NewAppointment(
            scenario,
            start,
            [SchedulingData.DiscountedItem(haircut, 10m, promotionId), SchedulingData.FixedItem(beard)],
            "Prefers the window chair");
        await SaveAsync(scenario.Company.Id, appointment, cancellationToken);

        var loaded = await LoadAsync(scenario.Company.Id, appointment.Id, cancellationToken);

        loaded.ShouldNotBeNull();
        loaded.CompanyId.ShouldBe(scenario.Company.Id);
        loaded.BranchId.ShouldBe(scenario.Branch.Id);
        loaded.CustomerId.ShouldBe(scenario.Customer.Id);
        loaded.ProfessionalId.ShouldBe(scenario.Professional.Id);
        loaded.Period.ShouldBe(DateTimeRange.Create(start, start.AddMinutes(75)));
        loaded.Period.Start.Kind.ShouldBe(DateTimeKind.Utc);
        loaded.Status.ShouldBe(AppointmentStatus.Confirmed);
        loaded.TotalPrice.ShouldBe(Money.From(130m));
        loaded.Notes.ShouldBe("Prefers the window chair");
        loaded.ServicesDuration.ShouldBe(TimeSpan.FromMinutes(75));
        loaded.OccupiesSchedule.ShouldBeTrue();

        loaded.Items.Count.ShouldBe(2);

        var haircutItem = loaded.Items.Single(item => item.ServiceOfferingId == haircut.Id);
        haircutItem.ServiceName.ShouldBe("Corte masculino");
        haircutItem.Duration.ShouldBe(TimeSpan.FromMinutes(45));
        haircutItem.IsOnRequest.ShouldBeFalse();
        haircutItem.ListPrice.ShouldBe(Money.From(100m));
        haircutItem.Discount.ShouldBe(Money.From(10m));
        haircutItem.FinalPrice.ShouldBe(Money.From(90m));
        haircutItem.PromotionId.ShouldBe(promotionId);
        haircutItem.IsFromBundle.ShouldBeFalse();

        var beardItem = loaded.Items.Single(item => item.ServiceOfferingId == beard.Id);
        beardItem.Duration.ShouldBe(TimeSpan.FromMinutes(30));
        beardItem.Discount.ShouldBe(Money.Zero);
        beardItem.FinalPrice.ShouldBe(Money.From(40m));
        beardItem.PromotionId.ShouldBeNull();

        var created = loaded.History.ShouldHaveSingleItem();
        created.From.ShouldBeNull();
        created.To.ShouldBe(AppointmentStatus.Confirmed);
        created.OccurredAtUtc.ShouldBe(SchedulingData.Now);
        created.OccurredAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
        created.ChangedBy.ShouldBe(SchedulingData.Actor);
        created.Reason.ShouldBeNull();
    }

    [Fact]
    public async Task A_service_on_request_should_wait_for_its_price_and_then_be_confirmed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SchedulingData.SeedAsync(mysql, cancellationToken);
        var haircut = CatalogData.NewFixedService(scenario.Company.Id, price: 100m, minutes: 45);
        var tattoo = CatalogData.NewOnRequestService(scenario.Company.Id);
        var start = SchedulingData.StartIn(3);

        var appointment = SchedulingData.NewAppointment(
            scenario, start, [SchedulingData.FixedItem(haircut), SchedulingData.OnRequestItem(tattoo)]);
        await SaveAsync(scenario.Company.Id, appointment, cancellationToken);

        var pending = await LoadAsync(scenario.Company.Id, appointment.Id, cancellationToken);

        pending.ShouldNotBeNull();
        pending.Status.ShouldBe(AppointmentStatus.Pending);
        pending.TotalPrice.ShouldBeNull();
        pending.HasPendingPrice.ShouldBeTrue();

        var pendingItem = pending.Items.Single(item => item.IsOnRequest);
        pendingItem.ListPrice.ShouldBeNull();
        pendingItem.FinalPrice.ShouldBeNull();
        pendingItem.HasPendingPrice.ShouldBeTrue();

        await UpdateAsync(scenario.Company.Id, appointment.Id, cancellationToken, loaded =>
        {
            loaded.SetItemPrice(pendingItem.Id, Money.From(60m), TimeSpan.FromMinutes(90));
            loaded.Confirm(SchedulingData.Actor, SchedulingData.Now.AddHours(1));
        });

        var confirmed = await LoadAsync(scenario.Company.Id, appointment.Id, cancellationToken);

        confirmed.ShouldNotBeNull();
        confirmed.Status.ShouldBe(AppointmentStatus.Confirmed);
        confirmed.HasPendingPrice.ShouldBeFalse();
        confirmed.TotalPrice.ShouldBe(Money.From(160m));
        confirmed.Period.ShouldBe(DateTimeRange.Create(start, start.AddMinutes(135))); // 45 + the 90 agreed

        var pricedItem = confirmed.Items.Single(item => item.IsOnRequest);
        pricedItem.ListPrice.ShouldBe(Money.From(60m));
        pricedItem.FinalPrice.ShouldBe(Money.From(60m));
        pricedItem.Duration.ShouldBe(TimeSpan.FromMinutes(90));

        // The database does not promise any order when reading the history: order it by when it happened.
        var history = confirmed.History.OrderBy(change => change.OccurredAtUtc).ToList();
        history.Count.ShouldBe(2);
        history[0].From.ShouldBeNull();
        history[0].To.ShouldBe(AppointmentStatus.Pending);
        history[1].From.ShouldBe(AppointmentStatus.Pending);
        history[1].To.ShouldBe(AppointmentStatus.Confirmed);
        history[1].OccurredAtUtc.ShouldBe(SchedulingData.Now.AddHours(1));
    }

    [Fact]
    public async Task Adding_and_removing_services_should_be_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SchedulingData.SeedAsync(mysql, cancellationToken);
        var haircut = CatalogData.NewFixedService(scenario.Company.Id, price: 100m, minutes: 45);
        var beard = CatalogData.NewFixedService(scenario.Company.Id, price: 40m, minutes: 30);
        var start = SchedulingData.StartIn(3);

        var appointment = SchedulingData.NewAppointment(scenario, start, [SchedulingData.FixedItem(haircut)]);
        await SaveAsync(scenario.Company.Id, appointment, cancellationToken);

        await UpdateAsync(scenario.Company.Id, appointment.Id, cancellationToken,
            loaded => loaded.AddItemByStaff(SchedulingData.Actor, SchedulingData.FixedItem(beard)));

        var withBoth = await LoadAsync(scenario.Company.Id, appointment.Id, cancellationToken);

        withBoth.ShouldNotBeNull();
        withBoth.Items.Count.ShouldBe(2);
        withBoth.TotalPrice.ShouldBe(Money.From(140m));
        withBoth.Period.ShouldBe(DateTimeRange.Create(start, start.AddMinutes(75)));

        await UpdateAsync(scenario.Company.Id, appointment.Id, cancellationToken, loaded =>
        {
            var haircutItem = loaded.Items.Single(item => item.ServiceOfferingId == haircut.Id);
            loaded.RemoveItemByStaff(SchedulingData.Actor, haircutItem.Id);
        });

        var onlyBeard = await LoadAsync(scenario.Company.Id, appointment.Id, cancellationToken);

        onlyBeard.ShouldNotBeNull();
        onlyBeard.Items.ShouldHaveSingleItem().ServiceOfferingId.ShouldBe(beard.Id);
        onlyBeard.TotalPrice.ShouldBe(Money.From(40m));
        onlyBeard.Period.ShouldBe(DateTimeRange.Create(start, start.AddMinutes(30)));
    }

    [Fact]
    public async Task Removing_a_service_of_a_bundle_should_save_the_other_at_its_list_price()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SchedulingData.SeedAsync(mysql, cancellationToken);
        var haircut = CatalogData.NewFixedService(scenario.Company.Id, price: 100m, minutes: 45);
        var beard = CatalogData.NewFixedService(scenario.Company.Id, price: 40m, minutes: 30);
        var promotionId = Guid.CreateVersion7();

        var appointment = SchedulingData.NewAppointment(
            scenario,
            SchedulingData.StartIn(3),
            [SchedulingData.BundleItem(haircut, 8m, promotionId), SchedulingData.BundleItem(beard, 2m, promotionId)]);
        await SaveAsync(scenario.Company.Id, appointment, cancellationToken);

        var bundled = await LoadAsync(scenario.Company.Id, appointment.Id, cancellationToken);

        bundled.ShouldNotBeNull();
        bundled.Items.ShouldAllBe(item => item.IsFromBundle && item.PromotionId == promotionId);
        bundled.TotalPrice.ShouldBe(Money.From(130m));

        await UpdateAsync(scenario.Company.Id, appointment.Id, cancellationToken, loaded =>
        {
            var beardItem = loaded.Items.Single(item => item.ServiceOfferingId == beard.Id);
            loaded.RemoveItemByStaff(SchedulingData.Actor, beardItem.Id);
        });

        var alone = await LoadAsync(scenario.Company.Id, appointment.Id, cancellationToken);

        alone.ShouldNotBeNull();
        var remaining = alone.Items.ShouldHaveSingleItem();
        remaining.ServiceOfferingId.ShouldBe(haircut.Id);
        remaining.IsFromBundle.ShouldBeFalse();
        remaining.PromotionId.ShouldBeNull();
        remaining.Discount.ShouldBe(Money.Zero);
        remaining.FinalPrice.ShouldBe(Money.From(100m));
        alone.TotalPrice.ShouldBe(Money.From(100m));
    }

    [Fact]
    public async Task Rescheduling_should_move_the_period()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SchedulingData.SeedAsync(mysql, cancellationToken);
        var haircut = CatalogData.NewFixedService(scenario.Company.Id, minutes: 45);
        var appointment = SchedulingData.NewAppointment(scenario, SchedulingData.StartIn(3), [SchedulingData.FixedItem(haircut)]);
        await SaveAsync(scenario.Company.Id, appointment, cancellationToken);

        var newStart = SchedulingData.StartIn(5, 10);

        await UpdateAsync(scenario.Company.Id, appointment.Id, cancellationToken, loaded =>
            loaded.RescheduleByStaff(SchedulingData.Actor, newStart, BookingPolicy.Default, SchedulingData.Now));

        var moved = await LoadAsync(scenario.Company.Id, appointment.Id, cancellationToken);

        moved.ShouldNotBeNull();
        moved.Period.ShouldBe(DateTimeRange.Create(newStart, newStart.AddMinutes(45)));
        moved.Status.ShouldBe(AppointmentStatus.Confirmed);
    }

    [Fact]
    public async Task Cancelling_should_save_the_status_and_the_reason_in_the_history()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SchedulingData.SeedAsync(mysql, cancellationToken);
        var haircut = CatalogData.NewFixedService(scenario.Company.Id);
        var appointment = SchedulingData.NewAppointment(scenario, SchedulingData.StartIn(3), [SchedulingData.FixedItem(haircut)]);
        await SaveAsync(scenario.Company.Id, appointment, cancellationToken);

        await UpdateAsync(scenario.Company.Id, appointment.Id, cancellationToken, loaded =>
            loaded.CancelByStaff(SchedulingData.Actor, SchedulingData.Now.AddHours(2), "Customer called"));

        var cancelled = await LoadAsync(scenario.Company.Id, appointment.Id, cancellationToken);

        cancelled.ShouldNotBeNull();
        cancelled.Status.ShouldBe(AppointmentStatus.Cancelled);
        cancelled.OccupiesSchedule.ShouldBeFalse();

        var history = cancelled.History.OrderBy(change => change.OccurredAtUtc).ToList();
        history.Count.ShouldBe(2);
        history[1].From.ShouldBe(AppointmentStatus.Confirmed);
        history[1].To.ShouldBe(AppointmentStatus.Cancelled);
        history[1].Reason.ShouldBe("Customer called");
    }

    [Fact]
    public async Task An_appointment_that_is_carried_out_should_keep_every_step_in_the_history()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SchedulingData.SeedAsync(mysql, cancellationToken);
        var haircut = CatalogData.NewFixedService(scenario.Company.Id, minutes: 45);
        var start = SchedulingData.StartIn(3);
        var appointment = SchedulingData.NewAppointment(scenario, start, [SchedulingData.FixedItem(haircut)]);
        await SaveAsync(scenario.Company.Id, appointment, cancellationToken);

        await UpdateAsync(scenario.Company.Id, appointment.Id, cancellationToken,
            loaded => loaded.Start(SchedulingData.Actor, start));
        await UpdateAsync(scenario.Company.Id, appointment.Id, cancellationToken,
            loaded => loaded.Complete(SchedulingData.Actor, start.AddMinutes(45)));

        var done = await LoadAsync(scenario.Company.Id, appointment.Id, cancellationToken);

        done.ShouldNotBeNull();
        done.Status.ShouldBe(AppointmentStatus.Completed);
        done.History.OrderBy(change => change.OccurredAtUtc).Select(change => change.To)
            .ShouldBe([AppointmentStatus.Confirmed, AppointmentStatus.InProgress, AppointmentStatus.Completed]);
    }

    [Fact]
    public async Task An_appointment_should_only_be_found_by_its_own_company()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SchedulingData.SeedAsync(mysql, cancellationToken);
        var other = await OrganizationDataSeedAsync(cancellationToken);
        var appointment = SchedulingData.NewAppointment(
            scenario, SchedulingData.StartIn(3), [SchedulingData.FixedItem(CatalogData.NewFixedService(scenario.Company.Id))]);
        await SaveAsync(scenario.Company.Id, appointment, cancellationToken);

        (await LoadAsync(scenario.Company.Id, appointment.Id, cancellationToken)).ShouldNotBeNull();
        (await LoadAsync(other.Id, appointment.Id, cancellationToken)).ShouldBeNull();
        (await LoadAsync(null, appointment.Id, cancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task An_appointment_must_refer_to_a_branch_a_customer_and_a_professional_that_exist()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SchedulingData.SeedAsync(mysql, cancellationToken);
        var items = new[] { SchedulingData.FixedItem(CatalogData.NewFixedService(scenario.Company.Id)) };
        var start = SchedulingData.StartIn(3);

        Appointment With(Guid branchId, Guid customerId, Guid professionalId) =>
            Appointment.Create(
                scenario.Company.Id, branchId, customerId, professionalId,
                start, items, BookingPolicy.Default, SchedulingData.Now, SchedulingData.Actor);

        var unknown = Guid.CreateVersion7();

        await Should.ThrowAsync<DbUpdateException>(() => SaveAsync(
            scenario.Company.Id, With(unknown, scenario.Customer.Id, scenario.Professional.Id), cancellationToken));
        await Should.ThrowAsync<DbUpdateException>(() => SaveAsync(
            scenario.Company.Id, With(scenario.Branch.Id, unknown, scenario.Professional.Id), cancellationToken));
        await Should.ThrowAsync<DbUpdateException>(() => SaveAsync(
            scenario.Company.Id, With(scenario.Branch.Id, scenario.Customer.Id, unknown), cancellationToken));
    }

    [Fact]
    public async Task The_largest_values_that_fit_in_the_columns_should_be_saved_as_they_are()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SchedulingData.SeedAsync(mysql, cancellationToken);
        var service = CatalogData.NewFixedService(
            scenario.Company.Id, price: 9_999_999_999.99m, minutes: 60, name: new string('S', 150));

        var appointment = SchedulingData.NewAppointment(
            scenario, SchedulingData.StartIn(3), [SchedulingData.FixedItem(service)], new string('N', 1000));
        await SaveAsync(scenario.Company.Id, appointment, cancellationToken);

        await UpdateAsync(scenario.Company.Id, appointment.Id, cancellationToken, loaded =>
            loaded.CancelByStaff(SchedulingData.Actor, SchedulingData.Now.AddHours(1), new string('R', 500)));

        var loadedAgain = await LoadAsync(scenario.Company.Id, appointment.Id, cancellationToken);

        loadedAgain.ShouldNotBeNull();
        loadedAgain.Notes!.Length.ShouldBe(1000);
        loadedAgain.TotalPrice.ShouldBe(Money.From(9_999_999_999.99m));
        loadedAgain.Items.ShouldHaveSingleItem().ServiceName.Length.ShouldBe(150);
        loadedAgain.History.Single(change => change.To == AppointmentStatus.Cancelled).Reason!.Length.ShouldBe(500);
    }

    private Task<Company> OrganizationDataSeedAsync(CancellationToken cancellationToken) =>
        OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

    private Task SaveAsync(Guid companyId, Appointment appointment, CancellationToken cancellationToken) =>
        StaffData.SaveAsync(mysql, companyId, cancellationToken,
            services => services.GetRequiredService<IAppointmentRepository>().Add(appointment));

    private async Task UpdateAsync(Guid companyId, Guid id, CancellationToken cancellationToken, Action<Appointment> change)
    {
        await using var scope = mysql.CreateScope(companyId);
        var loaded = await scope.ServiceProvider.GetRequiredService<IAppointmentRepository>().GetByIdAsync(id, cancellationToken);

        change(loaded!);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
    }

    private async Task<Appointment?> LoadAsync(Guid? companyId, Guid id, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);

        return await scope.ServiceProvider.GetRequiredService<IAppointmentRepository>().GetByIdAsync(id, cancellationToken);
    }
}