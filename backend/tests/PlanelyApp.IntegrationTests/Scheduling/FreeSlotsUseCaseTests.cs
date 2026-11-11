using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.BranchOfferings.SetAvailability;
using PlanelyApp.Application.Catalog.ServiceOfferings;
using PlanelyApp.Application.Catalog.ServiceOfferings.Create;
using PlanelyApp.Application.Catalog.ServiceOfferings.Deactivate;
using PlanelyApp.Application.Organization.Branches.Create;
using PlanelyApp.Application.Organization.Branches.SetBusinessHours;
using PlanelyApp.Application.Organization.Professionals.AddTimeOff;
using PlanelyApp.Application.Organization.Professionals.EnableOffering;
using PlanelyApp.Application.Organization.Professionals.Register;
using PlanelyApp.Application.Organization.Professionals.SetWorkingHours;
using PlanelyApp.Application.Organization.Shared;
using PlanelyApp.Application.Scheduling.Availability;
using PlanelyApp.Application.Scheduling.Availability.GetFreeSlots;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.IntegrationTests.Catalog;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Scheduling;

/// <summary>The whole way, as a request has it: dispatcher, decorators, handler, repositories, calculator and MySQL.</summary>
public class FreeSlotsUseCaseTests(MySqlFixture mysql)
{
    // The next Monday at least 4 days ahead: far enough for the notice, near enough for the window. São Paulo is UTC-3.
    private static readonly DateOnly Day = NextMonday();

    [Fact]
    public async Task A_free_day_should_offer_every_hour_of_the_working_period()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var setup = await SeedAsync(cancellationToken);

        var slots = await SlotsAsync(setup.Company, setup.Centro, [setup.Haircut], cancellationToken);

        slots.Date.ShouldBe(Day);
        var ana = slots.Professionals.Single();
        ana.ProfessionalId.ShouldBe(setup.Ana);
        ana.ProfessionalName.ShouldBe("Ana Souza");
        ana.ServicesDurationMinutes.ShouldBe(30);
        ana.StartsUtc.ShouldBe(Enumerable.Range(9, 9).Select(hour => At(hour)).ToList(), ignoreOrder: false);
    }

    [Fact]
    public async Task A_break_an_appointment_and_a_time_off_should_take_their_time_away()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var setup = await SeedAsync(cancellationToken);
        var lunch = new DailyScheduleData(
            Day.DayOfWeek,
            new TimeOnly(9, 0),
            new TimeOnly(18, 0),
            [new BreakData(new TimeOnly(12, 0), new TimeOnly(13, 0), "Lunch")]);
        await mysql.SendAsync(
            setup.Company, new SetProfessionalWorkingHoursCommand(setup.Ana, setup.Centro, lunch), cancellationToken);
        await mysql.SendAsync(
            setup.Company, new AddProfessionalTimeOffCommand(setup.Ana, At(15), At(16)), cancellationToken);
        await SaveAppointmentAsync(setup, setup.Centro, At(10), cancellationToken);

        var slots = await SlotsAsync(setup.Company, setup.Centro, [setup.Haircut], cancellationToken);

        slots.Professionals.Single().StartsUtc
            .ShouldBe(new[] { 9, 11, 13, 14, 16, 17 }.Select(hour => At(hour)).ToList(), ignoreOrder: false);
    }

    [Fact]
    public async Task An_appointment_at_another_branch_should_also_take_the_travel_time_away()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var setup = await SeedAsync(cancellationToken);
        await SaveAppointmentAsync(setup, setup.Norte, At(13), cancellationToken);

        var slots = await SlotsAsync(setup.Company, setup.Centro, [setup.Haircut], cancellationToken, step: 30);

        // The company of OrganizationData has an interval of 10 minutes and a travel time of 45, so a slot is
        // 30 + 10 = 40 minutes long and the appointment of 13:00 to 13:30 elsewhere blocks 12:15 to 14:15.
        // Starts at 12:00 (to 12:40) up to 14:00 overlap it; 17:30 (to 18:10) no longer fits before closing.
        var expected = Enumerable.Range(9, 9)
            .SelectMany(hour => new[] { At(hour), At(hour, 30) })
            .Where(start => start <= At(11, 30) || (start >= At(14, 30) && start <= At(17)))
            .ToList();
        slots.Professionals.Single().StartsUtc.ShouldBe(expected, ignoreOrder: false);
    }

    [Fact]
    public async Task The_professionals_should_be_filtered_by_branch_service_and_the_one_asked_for()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var setup = await SeedAsync(cancellationToken);
        var bruno = await CreateProfessionalAsync(setup.Company, "Bruno Lima", setup.Centro, setup.Haircut, cancellationToken);
        var carla = await CreateProfessionalAsync(setup.Company, "Carla Dias", setup.Centro, null, cancellationToken);
        var davi = await CreateProfessionalAsync(setup.Company, "Davi Reis", setup.Norte, setup.Haircut, cancellationToken);

        var everyone = await SlotsAsync(setup.Company, setup.Centro, [setup.Haircut], cancellationToken);
        var onlyBruno = await SlotsAsync(setup.Company, setup.Centro, [setup.Haircut], cancellationToken, professionalId: bruno);
        var withoutTheService = await SlotsAsync(setup.Company, setup.Centro, [setup.Haircut], cancellationToken, professionalId: carla);
        var otherBranch = await SlotsAsync(setup.Company, setup.Centro, [setup.Haircut], cancellationToken, professionalId: davi);

        everyone.Professionals.Select(professional => professional.ProfessionalName)
            .ShouldBe(["Ana Souza", "Bruno Lima"], ignoreOrder: false);
        onlyBruno.Professionals.Select(professional => professional.ProfessionalId).ShouldBe([bruno], ignoreOrder: false);
        withoutTheService.Professionals.ShouldBeEmpty();
        otherBranch.Professionals.ShouldBeEmpty();
    }

    [Fact]
    public async Task What_does_not_exist_or_cannot_be_booked_should_be_refused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var setup = await SeedAsync(cancellationToken);
        var other = await SeedAsync(cancellationToken);
        var inactive = await CreateServiceAsync(setup.Company, "Old cut", cancellationToken);
        await mysql.SendAsync(setup.Company, new DeactivateServiceOfferingCommand(inactive), cancellationToken);
        var notOffered = await CreateServiceAsync(setup.Company, "Not in Centro", cancellationToken);
        await mysql.SendAsync(
            setup.Company, new SetBranchOfferingAvailabilityCommand(setup.Centro, notOffered, false), cancellationToken);

        var unknownBranch = await TryAsync(setup.Company, Guid.CreateVersion7(), [setup.Haircut], cancellationToken);
        var anotherBranch = await TryAsync(setup.Company, other.Centro, [setup.Haircut], cancellationToken);
        var unknownService = await TryAsync(setup.Company, setup.Centro, [Guid.CreateVersion7()], cancellationToken);
        var anotherService = await TryAsync(setup.Company, setup.Centro, [setup.Haircut, other.Haircut], cancellationToken);
        var inactiveService = await TryAsync(setup.Company, setup.Centro, [inactive], cancellationToken);
        var notOfferedService = await TryAsync(setup.Company, setup.Centro, [notOffered], cancellationToken);
        var unknownProfessional = await mysql.QueryAsync(
            setup.Company,
            new GetFreeSlotsQuery(setup.Centro, Day, [setup.Haircut], Guid.CreateVersion7(), 60),
            cancellationToken);

        unknownBranch.Error.Code.ShouldBe("Branch.NotFound");
        anotherBranch.Error.Code.ShouldBe("Branch.NotFound");
        unknownService.Error.Code.ShouldBe("ServiceOffering.NotFound");
        anotherService.Error.Code.ShouldBe("ServiceOffering.NotFound");
        inactiveService.Error.Type.ShouldBe(ErrorType.Conflict);
        inactiveService.Error.Code.ShouldBe("ServiceOffering.NotAvailableInBranch");
        notOfferedService.Error.Type.ShouldBe(ErrorType.Conflict);
        notOfferedService.Error.Code.ShouldBe("ServiceOffering.NotAvailableInBranch");
        unknownProfessional.Error.Code.ShouldBe("Professional.NotFound");
    }

    private static DateOnly NextMonday()
    {
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(4);

        while (day.DayOfWeek != DayOfWeek.Monday)
        {
            day = day.AddDays(1);
        }

        return day;
    }

    private static DateTime At(int hour, int minute = 0) => Day.ToDateTime(new TimeOnly(hour + 3, minute), DateTimeKind.Utc);

    private async Task<FreeSlotsDto> SlotsAsync(
        Guid companyId,
        Guid branchId,
        Guid[] serviceIds,
        CancellationToken cancellationToken,
        int step = 60,
        Guid? professionalId = null) =>
        (await mysql.QueryAsync(
            companyId, new GetFreeSlotsQuery(branchId, Day, serviceIds, professionalId, step), cancellationToken)).Value;

    private Task<Result<FreeSlotsDto>> TryAsync(
        Guid companyId,
        Guid branchId,
        Guid[] serviceIds,
        CancellationToken cancellationToken) =>
        mysql.QueryAsync(companyId, new GetFreeSlotsQuery(branchId, Day, serviceIds, null, 60), cancellationToken);

    // A company with two branches open on that Monday from 9 to 18, a 30-minute haircut and Ana Souza at Centro.
    private async Task<Setup> SeedAsync(CancellationToken cancellationToken)
    {
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var centro = await CreateBranchAsync(company.Id, "Centro", cancellationToken);
        var norte = await CreateBranchAsync(company.Id, "Norte", cancellationToken);
        var haircut = await CreateServiceAsync(company.Id, "Haircut", cancellationToken);
        var ana = await CreateProfessionalAsync(company.Id, "Ana Souza", centro, haircut, cancellationToken);

        return new Setup(company.Id, centro, norte, haircut, ana);
    }

    private async Task<Guid> CreateBranchAsync(Guid companyId, string name, CancellationToken cancellationToken)
    {
        var branch = (await mysql.SendAsync(
            companyId,
            new CreateBranchCommand(
                name,
                new AddressData("Rua das Flores", "123", "Sala 4", "Centro", "São Paulo", "SP", "01310100"),
                "+551133334444",
                "America/Sao_Paulo"),
            cancellationToken)).Value;

        await mysql.SendAsync(
            companyId, new SetBranchBusinessHoursCommand(branch, WorkingDay()), cancellationToken);

        return branch;
    }

    private async Task<Guid> CreateServiceAsync(Guid companyId, string name, CancellationToken cancellationToken) =>
        (await mysql.SendAsync(
            companyId,
            new CreateServiceOfferingCommand(name, 30, new PricingData(PricingType.Fixed, 50m)),
            cancellationToken)).Value;

    private async Task<Guid> CreateProfessionalAsync(
        Guid companyId,
        string name,
        Guid branchId,
        Guid? serviceId,
        CancellationToken cancellationToken)
    {
        var professional = (await mysql.SendAsync(companyId, new RegisterProfessionalCommand(name), cancellationToken)).Value;

        await mysql.SendAsync(
            companyId, new SetProfessionalWorkingHoursCommand(professional, branchId, WorkingDay()), cancellationToken);

        if (serviceId is { } service)
        {
            await mysql.SendAsync(
                companyId, new EnableProfessionalOfferingCommand(professional, service), cancellationToken);
        }

        return professional;
    }

    private static DailyScheduleData WorkingDay() => new(Day.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(18, 0), []);

    // The appointment goes straight to the repository: creating appointments is the next sub-phase.
    private async Task SaveAppointmentAsync(Setup setup, Guid branchId, DateTime start, CancellationToken cancellationToken)
    {
        var customer = StaffData.NewCustomer(setup.Company);
        var service = CatalogData.NewFixedService(setup.Company);
        var appointment = Appointment.Create(
            setup.Company,
            branchId,
            customer.Id,
            setup.Ana,
            start,
            [SchedulingData.FixedItem(service)],
            BookingPolicy.Default,
            SchedulingData.Now,
            SchedulingData.Actor);

        await mysql.SaveAsync(setup.Company, cancellationToken, services =>
        {
            services.GetRequiredService<ICustomerRepository>().Add(customer);
            services.GetRequiredService<IAppointmentRepository>().Add(appointment);
        });
    }

    private sealed record Setup(Guid Company, Guid Centro, Guid Norte, Guid Haircut, Guid Ana);
}