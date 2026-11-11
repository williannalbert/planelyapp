using PlanelyApp.Application.Abstractions.Cqrs;

namespace PlanelyApp.Application.Organization.Professionals.AddTimeOff;

/// <summary>Blocks a period (UTC instants) in which the professional is unavailable. Returns the id of the time off.</summary>
public sealed record AddProfessionalTimeOffCommand(Guid ProfessionalId, DateTime StartUtc, DateTime EndUtc, string? Reason = null)
    : ICommand<Guid>;