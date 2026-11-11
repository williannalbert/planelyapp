using FluentValidation;

namespace PlanelyApp.Application.Organization.Shared;

internal sealed class DailyScheduleDataValidator : AbstractValidator<DailyScheduleData>
{
    public DailyScheduleDataValidator()
    {
        // The ranges and the breaks are checked by the Domain (DailySchedule.Create), with the code of each rule.
        RuleFor(schedule => schedule.Breaks).NotNull();
    }
}