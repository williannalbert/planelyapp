using PlanelyApp.Application.Abstractions.Cqrs;

namespace PlanelyApp.Application.Organization.Professionals.List;

/// <summary>The professionals of the company of the request. With <see cref="IsActive"/>, only the active or the inactive ones.</summary>
public sealed record ListProfessionalsQuery(bool? IsActive = null) : IQuery<IReadOnlyList<ProfessionalSummaryDto>>;