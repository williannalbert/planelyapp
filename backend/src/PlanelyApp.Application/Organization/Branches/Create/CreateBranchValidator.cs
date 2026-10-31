using FluentValidation;
using PlanelyApp.Application.Abstractions.Validation;
using PlanelyApp.Application.Organization.Shared;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Organization.Branches.Create;

internal sealed class CreateBranchValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchValidator()
    {
        RuleFor(command => command.Name).NotEmpty();
        RuleFor(command => command.Address).NotNull().SetValidator(new AddressDataValidator());
        RuleFor(command => command.Phone).NotEmpty().AcceptedBy(Phone.Create);
        RuleFor(command => command.TimeZoneId).NotEmpty();
    }
}