using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Application.Catalog.ServiceOfferings;
using PlanelyApp.Application.Catalog.ServiceOfferings.Create;
using PlanelyApp.Application.Organization.Branches.Create;
using PlanelyApp.Application.Organization.Branches.SetBusinessHours;
using PlanelyApp.Application.Organization.Professionals.EnableOffering;
using PlanelyApp.Application.Organization.Professionals.Register;
using PlanelyApp.Application.Organization.Professionals.SetWorkingHours;
using PlanelyApp.Application.Organization.Shared;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.IntegrationTests.Organization;

namespace PlanelyApp.IntegrationTests.Scheduling;

/// <summary>A company with two branches open on <see cref="BookingData.Day"/>, a haircut, Ana Souza, a customer and a user.</summary>
internal sealed record Booking(Guid Company, Guid Centro, Guid Norte, Guid Haircut, Guid Ana, Guid Customer, Guid Staff);

internal static class BookingData
{
    // The next Monday at least 4 days ahead: far enough for the notice of the company. São Paulo is UTC-3.
    public static readonly DateOnly Day = NextMonday();

    /// <summary>A local time of <see cref="Day"/> (hour up to 20), as a UTC instant.</summary>
    public static DateTime At(int hour, int minute = 0) => Day.ToDateTime(new TimeOnly(hour + 3, minute), DateTimeKind.Utc);

    public static async Task<Booking> SeedAsync(MySqlFixture mysql, CancellationToken cancellationToken)
    {
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var centro = await CreateBranchAsync(mysql, company.Id, "Centro", cancellationToken);
        var norte = await CreateBranchAsync(mysql, company.Id, "Norte", cancellationToken);
        var haircut = await CreateServiceAsync(mysql, company.Id, "Haircut", new PricingData(PricingType.Fixed, 50m), cancellationToken);
        var ana = await CreateProfessionalAsync(mysql, company.Id, "Ana Souza", centro, haircut, cancellationToken);

        var customer = StaffData.NewCustomer(company.Id);
        var staff = StaffData.NewUser(company.Id);
        staff.SetBranchAccess(true);

        await mysql.SaveAsync(company.Id, cancellationToken, services =>
        {
            services.GetRequiredService<ICustomerRepository>().Add(customer);
            services.GetRequiredService<IUserRepository>().Add(staff);
        });

        return new Booking(company.Id, centro, norte, haircut, ana, customer.Id, staff.Id);
    }

    public static async Task<Guid> CreateBranchAsync(
        MySqlFixture mysql,
        Guid companyId,
        string name,
        CancellationToken cancellationToken)
    {
        var branch = (await mysql.SendAsync(
            companyId,
            new CreateBranchCommand(
                name,
                new AddressData("Rua das Flores", "123", "Sala 4", "Centro", "São Paulo", "SP", "01310100"),
                "+551133334444",
                "America/Sao_Paulo"),
            cancellationToken)).Value;

        await mysql.SendAsync(companyId, new SetBranchBusinessHoursCommand(branch, WorkingDay()), cancellationToken);

        return branch;
    }

    public static async Task<Guid> CreateServiceAsync(
        MySqlFixture mysql,
        Guid companyId,
        string name,
        PricingData pricing,
        CancellationToken cancellationToken) =>
        (await mysql.SendAsync(
            companyId,
            new CreateServiceOfferingCommand(name, pricing.Type == PricingType.Fixed ? 30 : 120, pricing),
            cancellationToken)).Value;

    public static async Task<Guid> CreateProfessionalAsync(
        MySqlFixture mysql,
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

    private static DateOnly NextMonday()
    {
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(4);

        while (day.DayOfWeek != DayOfWeek.Monday)
        {
            day = day.AddDays(1);
        }

        return day;
    }
}