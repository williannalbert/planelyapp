namespace PlanelyApp.Application.Scheduling.Availability;

/// <summary>
/// The times a professional can start the services asked, in UTC. <see cref="ServicesDurationMinutes"/> is the sum of
/// the durations of the services for this professional (the policy interval is not in it).
/// </summary>
public sealed record ProfessionalSlotsDto(
    Guid ProfessionalId,
    string ProfessionalName,
    int ServicesDurationMinutes,
    IReadOnlyList<DateTime> StartsUtc);