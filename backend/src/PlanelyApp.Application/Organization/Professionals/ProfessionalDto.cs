namespace PlanelyApp.Application.Organization.Professionals;

public sealed record ProfessionalDto(
    Guid Id,
    string Name,
    string? Specialty,
    Guid? UserId,
    bool IsActive,
    IReadOnlyList<WorkingHoursData> WorkingHours,
    IReadOnlyList<TimeOffData> TimeOffs,
    IReadOnlyList<OfferingData> Offerings);