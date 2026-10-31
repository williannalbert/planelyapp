using NSubstitute;
using PlanelyApp.Application.Organization.Shared;
using PlanelyApp.Domain.Organization.Professionals;

namespace PlanelyApp.Application.Tests.Organization.Professionals;

internal static class ProfessionalData
{
    public static Professional NewProfessional(Guid? companyId = null, string name = "Ana Souza") =>
        Professional.Create(companyId ?? Guid.NewGuid(), name, "Cabeleireira");

    /// <summary>A repository that finds this professional (or none) whatever the id.</summary>
    public static IProfessionalRepository RepositoryWith(Professional? professional)
    {
        var professionals = Substitute.For<IProfessionalRepository>();
        professionals.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(professional);

        return professionals;
    }

    public static DailyScheduleData Day(DayOfWeek day, int fromHour, int toHour, params BreakData[] breaks) =>
        new(day, new TimeOnly(fromHour, 0), new TimeOnly(toHour, 0), breaks);
}