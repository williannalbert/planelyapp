using FluentValidation;
using PlanelyApp.Application.Organization.Shared;

namespace PlanelyApp.Application.Organization.Professionals.SetWorkingHours;

internal sealed class SetProfessionalWorkingHoursValidator : AbstractValidator<SetProfessionalWorkingHoursCommand>
{
    public SetProfessionalWorkingHoursValidator()
    {
        RuleFor(command => command.BranchId).NotEmpty();
        RuleFor(command => command.Schedule).NotNull().SetValidator(new DailyScheduleDataValidator());
    }
}