using FluentValidation;
using PlanelyApp.Application.Abstractions.Validation;
using PlanelyApp.Domain.Identity.AccessCodes;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Identity.AccessCodes.Verify;

internal sealed class VerifyAccessCodeValidator : AbstractValidator<VerifyAccessCodeCommand>
{
    public VerifyAccessCodeValidator()
    {
        RuleFor(command => command.Channel).IsInEnum().WithErrorCode(ValidationCodes.Invalid);
        RuleFor(command => command.Destination).NotEmpty();
        RuleFor(command => command.Destination).AcceptedBy(Email.Create).When(command => command.Channel == AccessChannel.Email);
        RuleFor(command => command.Destination).AcceptedBy(Phone.Create).When(command => command.Channel == AccessChannel.Phone);

        RuleFor(command => command.Code).NotEmpty();

        // A code that cannot be right (not 6 digits) is a 400 and costs no attempt: a typo should not burn one of the five.
        RuleFor(command => command.Code)
            .Must(IsWellFormed)
            .WithErrorCode(ValidationCodes.Invalid)
            .When(command => !string.IsNullOrWhiteSpace(command.Code));
    }

    private static bool IsWellFormed(string? code)
    {
        var candidate = code?.Trim();

        return candidate is { Length: AccessCode.CodeLength } && candidate.All(char.IsAsciiDigit);
    }
}