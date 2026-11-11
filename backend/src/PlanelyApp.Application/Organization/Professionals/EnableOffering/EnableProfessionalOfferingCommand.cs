using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Professionals.EnableOffering;

/// <summary>
/// The professional performs the service. Without <see cref="CustomDurationMinutes"/> the standard duration of the
/// service applies. Enabling a service that is already enabled changes its duration.
/// </summary>
public sealed record EnableProfessionalOfferingCommand(Guid ProfessionalId, Guid ServiceOfferingId, int? CustomDurationMinutes = null)
    : ICommand<Unit>;