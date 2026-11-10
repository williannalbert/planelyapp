using FluentValidation;
using PlanelyApp.Application.Abstractions.Validation;
using PlanelyApp.Domain.Identity.AccessCodes;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Identity.AccessCodes.Request;

internal sealed class RequestAccessCodeValidator : AbstractValidator<RequestAccessCodeCommand>
{
    public RequestAccessCodeValidator()
    {
        RuleFor(command => command.Channel).IsInEnum().WithErrorCode(ValidationCodes.Invalid);
        RuleFor(command => command.Destination).NotEmpty();
        RuleFor(command => command.Destination).AcceptedBy(Email.Create).When(command => command.Channel == AccessChannel.Email);
        RuleFor(command => command.Destination).AcceptedBy(Phone.Create).When(command => command.Channel == AccessChannel.Phone);
    }
}