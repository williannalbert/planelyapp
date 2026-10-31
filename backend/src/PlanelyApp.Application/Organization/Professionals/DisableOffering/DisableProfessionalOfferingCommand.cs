using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Professionals.DisableOffering;

/// <summary>The professional stops performing the service. Nothing happens if it was not enabled.</summary>
public sealed record DisableProfessionalOfferingCommand(Guid ProfessionalId, Guid ServiceOfferingId) : ICommand<Unit>;