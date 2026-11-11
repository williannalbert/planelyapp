using PlanelyApp.Application.Organization.Shared;

namespace PlanelyApp.Application.Organization.Branches;

public sealed record BranchDto(
    Guid Id,
    string Name,
    AddressData Address,
    string Phone,
    string TimeZoneId,
    bool IsActive,
    IReadOnlyList<DailyScheduleData> BusinessHours);