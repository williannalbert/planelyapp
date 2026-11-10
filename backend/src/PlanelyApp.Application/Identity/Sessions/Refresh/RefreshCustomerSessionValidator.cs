using FluentValidation;

namespace PlanelyApp.Application.Identity.Sessions.Refresh;

internal sealed class RefreshCustomerSessionValidator : AbstractValidator<RefreshCustomerSessionCommand>
{
    public RefreshCustomerSessionValidator() => RuleFor(command => command.RefreshToken).NotEmpty();
}