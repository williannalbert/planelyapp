using PlanelyApp.Api.Errors;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Api.Endpoints.Organization;

/// <summary>The weekday of a route. The model binder turns "9" into a <see cref="DayOfWeek"/> that is not one.</summary>
internal static class WeekdayRoute
{
    public static bool IsValid(DayOfWeek day) => Enum.IsDefined(day);

    /// <summary>A 400 on the field <c>day</c>.</summary>
    public static IResult Invalid() =>
        new ErrorResult(new ValidationError([new FieldError("day", "Validation.Invalid")]));
}