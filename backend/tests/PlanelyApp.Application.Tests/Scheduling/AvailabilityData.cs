using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.SharedKernel;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;
using CompanyData = PlanelyApp.Application.Tests.Organization.Companies.CompanyData;
using ProfessionalData = PlanelyApp.Application.Tests.Organization.Professionals.ProfessionalData;

namespace PlanelyApp.Application.Tests.Scheduling;

internal static class AvailabilityData
{
    // 2027-11-22 is a Monday. The branch is in São Paulo (UTC-3): 09:00 local is 12:00 UTC.
    public static readonly DateOnly Monday = new(2027, 11, 22);
    public static readonly DateTime Now = new(2027, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>A local time of the Monday above, as a UTC instant.</summary>
    public static DateTime At(int hour, int minute = 0) =>
        Monday.ToDateTime(new TimeOnly(hour + 3, minute), DateTimeKind.Utc);

    public static DailySchedule Hours(int fromHour, int toHour) =>
        DailySchedule.Create(
            DayOfWeek.Monday,
            TimeOfDayRange.Create(new TimeOnly(fromHour, 0), new TimeOnly(toHour, 0)));

    /// <summary>A branch open on Mondays from 9 to 18.</summary>
    public static Branch OpenBranch(Guid companyId)
    {
        var branch = BranchData.NewBranch(companyId);
        branch.SetBusinessHours(Hours(9, 18));

        return branch;
    }

    /// <summary>A professional who works at the branch on Mondays from 9 to 18 and performs these services.</summary>
    public static Professional WorkingProfessional(
        Guid companyId,
        Branch branch,
        string name,
        params ServiceOffering[] performs)
    {
        var professional = ProfessionalData.NewProfessional(companyId, name);
        professional.SetWorkingHours(branch.Id, Hours(9, 18));

        foreach (var service in performs)
        {
            professional.EnableOffering(service.Id);
        }

        return professional;
    }

    public static Company NewCompany(BookingPolicy? policy = null)
    {
        var company = CompanyData.NewCompany();

        if (policy is not null)
        {
            company.UpdatePolicy(policy);
        }

        return company;
    }

    /// <summary>The start times from this local hour to that one, every hour.</summary>
    public static List<DateTime> EveryHour(int fromHour, int toHour) =>
        Enumerable.Range(fromHour, toHour - fromHour + 1).Select(hour => At(hour)).ToList();
}

internal sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
}