using PlanelyApp.Application.Organization.Shared;

namespace PlanelyApp.Api.Endpoints.Organization.Shared;

/// <summary>The weekday of a route. The model binder turns "9" into a <see cref="DayOfWeek"/> that is not one.</summary>
internal static class WeekdayRoute
{
    public static bool IsValid(DayOfWeek day) => Enum.IsDefined(day);

    /// <summary>A 400 on the field of the day, named as the validators name it (<c>Day</c>).</summary>
    public static IResult Invalid() => FieldProblems.Invalid(nameof(DailyScheduleData.Day));
}