using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.Scheduling.Pricing;
using PlanelyApp.Domain.SharedKernel;
using PlanelyApp.IntegrationTests.Organization;

namespace PlanelyApp.IntegrationTests.Scheduling;

/// <summary>A company with a branch, a customer and a professional already saved: what an appointment refers to.</summary>
internal sealed record Scenario(Company Company, Branch Branch, Customer Customer, Professional Professional);

internal static class SchedulingData
{
    // The Domain receives the time as a parameter. Whole hours come back from MySQL exactly as they went in
    // (datetime(6) keeps microseconds, a DateTime keeps 100 nanoseconds).
    public static readonly DateTime Now = DateTime.UtcNow.Date.AddHours(8);

    /// <summary>The user who makes the changes in the tests.</summary>
    public static readonly Guid Actor = Guid.CreateVersion7();

    public static DateTime StartIn(int days, int hour = 14) => Now.Date.AddDays(days).AddHours(hour);

    public static async Task<Scenario> SeedAsync(MySqlFixture mysql, CancellationToken cancellationToken)
    {
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var branch = await StaffData.SeedBranchAsync(mysql, company.Id, cancellationToken);
        var customer = StaffData.NewCustomer(company.Id);
        var professional = Professional.Create(company.Id, "Ana Souza");

        await StaffData.SaveAsync(mysql, company.Id, cancellationToken, services =>
        {
            services.GetRequiredService<ICustomerRepository>().Add(customer);
            services.GetRequiredService<IProfessionalRepository>().Add(professional);
        });

        return new Scenario(company, branch, customer, professional);
    }

    /// <summary>Another professional of the same company, already saved.</summary>
    public static async Task<Professional> SeedProfessionalAsync(
        MySqlFixture mysql,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var professional = Professional.Create(companyId, "Bruno Lima");

        await StaffData.SaveAsync(mysql, companyId, cancellationToken,
            services => services.GetRequiredService<IProfessionalRepository>().Add(professional));

        return professional;
    }

    public static Appointment NewAppointment(
        Scenario scenario,
        DateTime start,
        NewAppointmentItem[] items,
        string? notes = null,
        Professional? professional = null) =>
        Appointment.Create(
            scenario.Company.Id,
            scenario.Branch.Id,
            scenario.Customer.Id,
            (professional ?? scenario.Professional).Id,
            start,
            items,
            BookingPolicy.Default,
            Now,
            Actor,
            notes);

    public static NewAppointmentItem FixedItem(ServiceOffering service) =>
        new(service, service.StandardDuration, PriceQuote.Fixed(service.BasePrice!));

    public static NewAppointmentItem DiscountedItem(ServiceOffering service, decimal discount, Guid promotionId) =>
        new(service, service.StandardDuration, PriceQuote.Fixed(service.BasePrice!, Money.From(discount), promotionId));

    public static NewAppointmentItem BundleItem(ServiceOffering service, decimal share, Guid promotionId) =>
        new(service, service.StandardDuration, PriceQuote.FromBundle(service.BasePrice!, Money.From(share), promotionId));

    public static NewAppointmentItem OnRequestItem(ServiceOffering service) =>
        new(service, service.StandardDuration, PriceQuote.OnRequest());
}