namespace PlanelyApp.Application.Organization.Professionals;

public sealed record ProfessionalSummaryDto(Guid Id, string Name, string? Specialty, bool IsActive);