using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.IntegrationTests.Catalog;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Scheduling;

public class AvailabilityRepositoryTests(MySqlFixture mysql)
{
    [Fact]
    public async Task Only_appointments_that_occupy_the_agenda_inside_the_window_of_the_professionals_asked_should_come_back()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SchedulingData.SeedAsync(mysql, cancellationToken);
        var bruno = await SchedulingData.SeedProfessionalAsync(mysql, scenario.Company.Id, cancellationToken);
        var item = SchedulingData.FixedItem(CatalogData.NewFixedService(scenario.Company.Id));
        var inside = SchedulingData.NewAppointment(scenario, SchedulingData.StartIn(5, 10), [item]);
        var cancelled = SchedulingData.NewAppointment(scenario, SchedulingData.StartIn(5, 12), [item]);
        cancelled.CancelByStaff(SchedulingData.Actor, SchedulingData.Now);
        var otherDay = SchedulingData.NewAppointment(scenario, SchedulingData.StartIn(9, 10), [item]);
        var ofBruno = SchedulingData.NewAppointment(scenario, SchedulingData.StartIn(5, 11), [item], null, bruno);
        foreach (var appointment in new[] { inside, cancelled, otherDay, ofBruno })
        {
            await SaveAsync(scenario.Company.Id, appointment, cancellationToken);
        }

        var onlyAna = await ListAsync(
            scenario.Company.Id, [scenario.Professional.Id], SchedulingData.StartIn(5, 0), SchedulingData.StartIn(6, 0), cancellationToken);
        var both = await ListAsync(
            scenario.Company.Id, [scenario.Professional.Id, bruno.Id], SchedulingData.StartIn(5, 0), SchedulingData.StartIn(6, 0), cancellationToken);

        onlyAna.ShouldBe([inside.Id], ignoreOrder: true);
        both.ShouldBe([inside.Id, ofBruno.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task An_appointment_that_only_touches_an_end_of_the_window_should_be_left_out()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SchedulingData.SeedAsync(mysql, cancellationToken);
        var item = SchedulingData.FixedItem(CatalogData.NewFixedService(scenario.Company.Id)); // 30 minutes
        var start = SchedulingData.StartIn(5, 10);
        var appointment = SchedulingData.NewAppointment(scenario, start, [item]);
        await SaveAsync(scenario.Company.Id, appointment, cancellationToken);
        var ana = new[] { scenario.Professional.Id };

        var endsWhereItStarts = await ListAsync(scenario.Company.Id, ana, start.AddHours(-2), start, cancellationToken);
        var startsWhereItEnds = await ListAsync(scenario.Company.Id, ana, start.AddMinutes(30), start.AddHours(2), cancellationToken);
        var overlapping = await ListAsync(scenario.Company.Id, ana, start.AddMinutes(29), start.AddHours(2), cancellationToken);

        endsWhereItStarts.ShouldBeEmpty();
        startsWhereItEnds.ShouldBeEmpty();
        overlapping.ShouldBe([appointment.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task The_appointments_of_another_company_should_not_come_back()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SchedulingData.SeedAsync(mysql, cancellationToken);
        var other = await SchedulingData.SeedAsync(mysql, cancellationToken);
        var mine = SchedulingData.NewAppointment(
            scenario, SchedulingData.StartIn(5, 10), [SchedulingData.FixedItem(CatalogData.NewFixedService(scenario.Company.Id))]);
        var theirs = SchedulingData.NewAppointment(
            other, SchedulingData.StartIn(5, 10), [SchedulingData.FixedItem(CatalogData.NewFixedService(other.Company.Id))]);
        await SaveAsync(scenario.Company.Id, mine, cancellationToken);
        await SaveAsync(other.Company.Id, theirs, cancellationToken);

        var found = await ListAsync(
            scenario.Company.Id,
            [scenario.Professional.Id, other.Professional.Id],
            SchedulingData.StartIn(5, 0),
            SchedulingData.StartIn(6, 0),
            cancellationToken);

        found.ShouldBe([mine.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task Professionals_at_a_branch_should_be_the_active_ones_with_hours_there_by_name()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = await SchedulingData.SeedAsync(mysql, cancellationToken); // Ana Souza has no working hours
        var centro = scenario.Branch.Id;
        var norte = OrganizationData.NewBranch(scenario.Company.Id, "Norte");
        await mysql.SaveAsync(scenario.Company.Id, cancellationToken,
            services => services.GetRequiredService<IBranchRepository>().Add(norte));

        var zeca = NewProfessional(scenario.Company.Id, "Zeca", centro);
        var ana = NewProfessional(scenario.Company.Id, "Ana", centro);
        var davi = NewProfessional(scenario.Company.Id, "Davi", norte.Id);
        var carla = NewProfessional(scenario.Company.Id, "Carla", centro);
        carla.Deactivate();
        await mysql.SaveAsync(scenario.Company.Id, cancellationToken, services =>
        {
            var professionals = services.GetRequiredService<IProfessionalRepository>();
            professionals.Add(zeca);
            professionals.Add(ana);
            professionals.Add(davi);
            professionals.Add(carla);
        });

        await using var scope = mysql.CreateScope(scenario.Company.Id);
        var found = await scope.ServiceProvider.GetRequiredService<IProfessionalRepository>()
            .ListActiveAtBranchAsync(centro, cancellationToken);

        found.Select(professional => professional.Name).ShouldBe(["Ana", "Zeca"], ignoreOrder: false);
        found.First().WorkingHours.ShouldNotBeEmpty();
    }

    private static Professional NewProfessional(Guid companyId, string name, Guid branchId)
    {
        var professional = Professional.Create(companyId, name);
        professional.SetWorkingHours(branchId, OrganizationData.WorkDay(DayOfWeek.Monday));

        return professional;
    }

    private Task SaveAsync(Guid companyId, Appointment appointment, CancellationToken cancellationToken) =>
        mysql.SaveAsync(companyId, cancellationToken,
            services => services.GetRequiredService<IAppointmentRepository>().Add(appointment));

    private async Task<IReadOnlyList<Guid>> ListAsync(
        Guid companyId,
        Guid[] professionalIds,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);
        var found = await scope.ServiceProvider.GetRequiredService<IAppointmentRepository>()
            .ListOccupyingScheduleAsync(professionalIds, fromUtc, toUtc, cancellationToken);

        return found.Select(appointment => appointment.Id).ToList();
    }
}