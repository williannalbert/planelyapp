using PlanelyApp.Application.Organization.Shared;

namespace PlanelyApp.Api.Endpoints.Organization;

/// <summary>
/// The hours of one weekday in a route like <c>.../business-hours/{day}</c> or <c>.../working-hours/{day}</c>.
/// No day: it is the one of the route. No list means no breaks.
/// </summary>
internal sealed record DailyScheduleBody(TimeOnly Start, TimeOnly End, IReadOnlyList<BreakData>? Breaks = null);