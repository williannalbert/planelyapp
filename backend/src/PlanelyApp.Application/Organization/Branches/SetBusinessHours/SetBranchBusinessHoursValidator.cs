using FluentValidation;
using PlanelyApp.Application.Organization.Shared;

namespace PlanelyApp.Application.Organization.Branches.SetBusinessHours;

internal sealed class SetBranchBusinessHoursValidator : AbstractValidator<SetBranchBusinessHoursCommand>
{
    public SetBranchBusinessHoursValidator()
    {
        RuleFor(command => command.Schedule).NotNull().SetValidator(new DailyScheduleDataValidator());
    }
}