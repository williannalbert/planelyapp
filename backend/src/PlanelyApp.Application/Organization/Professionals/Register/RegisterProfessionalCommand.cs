using PlanelyApp.Application.Abstractions.Cqrs;

namespace PlanelyApp.Application.Organization.Professionals.Register;

/// <summary>Registers a professional in the company of the request.</summary>
public sealed record RegisterProfessionalCommand(string Name, string? Specialty = null) : ICommand<Guid>;