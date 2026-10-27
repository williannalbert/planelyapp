using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.IntegrationTests.Catalog;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Scheduling;

public class AppointmentConcurrencyTests(MySqlFixture mysql)
{
    [Fact]
    public async Task Two_simultaneous_bookings_for_the_same_professional_should_not_both_be_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SchedulingData.SeedAsync(mysql, cancellationToken);
        var haircut = CatalogData.NewFixedService(scenario.Company.Id);
        var start = SchedulingData.StartIn(3);

        // The same professional at the same time: both requests found the slot free.
        var firstBooking = SchedulingData.NewAppointment(scenario, start, [SchedulingData.FixedItem(haircut)]);
        var secondBooking = SchedulingData.NewAppointment(scenario, start, [SchedulingData.FixedItem(haircut)]);

        await using var first = mysql.CreateScope(scenario.Company.Id);
        await using var second = mysql.CreateScope(scenario.Company.Id);

        // Both read the agenda of the professional at the same version.
        var seenByFirst = await first.ServiceProvider.GetRequiredService<IProfessionalRepository>()
            .GetByIdAsync(scenario.Professional.Id, cancellationToken);
        var seenBySecond = await second.ServiceProvider.GetRequiredService<IProfessionalRepository>()
            .GetByIdAsync(scenario.Professional.Id, cancellationToken);

        // What the booking use case does (RN27): add the appointment and raise the version, in one transaction.
        seenByFirst!.IncrementScheduleVersion();
        first.ServiceProvider.GetRequiredService<IAppointmentRepository>().Add(firstBooking);
        await first.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);

        seenBySecond!.IncrementScheduleVersion();
        second.ServiceProvider.GetRequiredService<IAppointmentRepository>().Add(secondBooking);

        await Should.ThrowAsync<DbUpdateConcurrencyException>(
            () => second.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken));

        // The loser's transaction was undone as a whole: its appointment does not exist.
        (await LoadAppointmentAsync(scenario.Company.Id, firstBooking.Id, cancellationToken)).ShouldNotBeNull();
        (await LoadAppointmentAsync(scenario.Company.Id, secondBooking.Id, cancellationToken)).ShouldBeNull();

        var professional = await LoadProfessionalAsync(scenario.Company.Id, scenario.Professional.Id, cancellationToken);
        professional!.ScheduleVersion.ShouldBe(scenario.Professional.ScheduleVersion + 1);
    }

    [Fact]
    public async Task Bookings_for_different_professionals_should_not_conflict()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SchedulingData.SeedAsync(mysql, cancellationToken);
        var other = await SchedulingData.SeedProfessionalAsync(mysql, scenario.Company.Id, cancellationToken);
        var haircut = CatalogData.NewFixedService(scenario.Company.Id);
        var start = SchedulingData.StartIn(3);

        var firstBooking = SchedulingData.NewAppointment(scenario, start, [SchedulingData.FixedItem(haircut)]);
        var secondBooking = SchedulingData.NewAppointment(scenario, start, [SchedulingData.FixedItem(haircut)], professional: other);

        await using var first = mysql.CreateScope(scenario.Company.Id);
        await using var second = mysql.CreateScope(scenario.Company.Id);

        var seenByFirst = await first.ServiceProvider.GetRequiredService<IProfessionalRepository>()
            .GetByIdAsync(scenario.Professional.Id, cancellationToken);
        var seenBySecond = await second.ServiceProvider.GetRequiredService<IProfessionalRepository>()
            .GetByIdAsync(other.Id, cancellationToken);

        seenByFirst!.IncrementScheduleVersion();
        first.ServiceProvider.GetRequiredService<IAppointmentRepository>().Add(firstBooking);
        await first.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);

        seenBySecond!.IncrementScheduleVersion();
        second.ServiceProvider.GetRequiredService<IAppointmentRepository>().Add(secondBooking);
        await second.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);

        (await LoadAppointmentAsync(scenario.Company.Id, firstBooking.Id, cancellationToken)).ShouldNotBeNull();
        (await LoadAppointmentAsync(scenario.Company.Id, secondBooking.Id, cancellationToken)).ShouldNotBeNull();
    }

    private async Task<Appointment?> LoadAppointmentAsync(Guid companyId, Guid id, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);

        return await scope.ServiceProvider.GetRequiredService<IAppointmentRepository>().GetByIdAsync(id, cancellationToken);
    }

    private async Task<Professional?> LoadProfessionalAsync(Guid companyId, Guid id, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);

        return await scope.ServiceProvider.GetRequiredService<IProfessionalRepository>().GetByIdAsync(id, cancellationToken);
    }
}