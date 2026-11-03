namespace PlanelyApp.Application.Scheduling.Availability;

/// <summary>The professionals who can do all the services, by name, each with the start times of the local day.</summary>
public sealed record FreeSlotsDto(DateOnly Date, IReadOnlyList<ProfessionalSlotsDto> Professionals);