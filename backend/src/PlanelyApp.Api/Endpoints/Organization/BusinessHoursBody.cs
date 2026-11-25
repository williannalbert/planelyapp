using PlanelyApp.Application.Organization.Shared;

namespace PlanelyApp.Api.Endpoints.Organization;

/// <summary>The body of <c>PUT /branches/{branchId}/business-hours/{day}</c>. No day: it is the one of the route. No list means no breaks.</summary>
internal sealed record BusinessHoursBody(TimeOnly Start, TimeOnly End, IReadOnlyList<BreakData>? Breaks = null);