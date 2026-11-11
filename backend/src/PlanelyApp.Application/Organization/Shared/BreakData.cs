namespace PlanelyApp.Application.Organization.Shared;

public sealed record BreakData(TimeOnly Start, TimeOnly End, string? Description);