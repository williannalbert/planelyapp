using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.IntegrationTests.Catalog;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Scheduling;

public class AppointmentStatusConcurrencyTests(MySqlFixture mysql)
{
    [Fact]
    public async Task Two_changes_of_status_read_at_the_same_state_should_not_both_be_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SchedulingData.SeedAsync(mysql, cancellationToken);
        var service = CatalogData.NewFixedService(scenario.Company.Id);

        // Born pending: this policy asks for confirmation.
        var appointment = Appointment.Create(
            scenario.Company.Id,
            scenario.Branch.Id,
            scenario.Customer.Id,
            scenario.Professional.Id,
            SchedulingData.StartIn(3),
            [SchedulingData.FixedItem(service)],
            BookingPolicy.Create(2, 60, 24, 0, 30, true),
            SchedulingData.Now,
            SchedulingData.Actor);
        await mysql.SaveAsync(scenario.Company.Id, cancellationToken,
            services => services.GetRequiredService<IAppointmentRepository>().Add(appointment));

        await using var first = mysql.CreateScope(scenario.Company.Id);
        await using var second = mysql.CreateScope(scenario.Company.Id);
        var seenByFirst = await first.ServiceProvider.GetRequiredService<IAppointmentRepository>()
            .GetByIdAsync(appointment.Id, cancellationToken);
        var seenBySecond = await second.ServiceProvider.GetRequiredService<IAppointmentRepository>()
            .GetByIdAsync(appointment.Id, cancellationToken);

        seenByFirst!.Confirm(SchedulingData.Actor, SchedulingData.Now);
        await first.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);

        seenBySecond!.CancelByStaff(SchedulingData.Actor, SchedulingData.Now);

        await Should.ThrowAsync<DbUpdateConcurrencyException>(
            () => second.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken));

        // The loser's transaction was undone as a whole: the appointment is confirmed, with two lines of history.
        await using var check = mysql.CreateScope(scenario.Company.Id);
        var saved = await check.ServiceProvider.GetRequiredService<IAppointmentRepository>()
            .GetByIdAsync(appointment.Id, cancellationToken);
        saved.ShouldNotBeNull();
        saved.Status.ShouldBe(AppointmentStatus.Confirmed);
        saved.History.Count.ShouldBe(2);
    }
}