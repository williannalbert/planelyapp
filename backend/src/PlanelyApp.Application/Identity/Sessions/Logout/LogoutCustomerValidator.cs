using FluentValidation;

namespace PlanelyApp.Application.Identity.Sessions.Logout;

internal sealed class LogoutCustomerValidator : AbstractValidator<LogoutCustomerCommand>
{
    public LogoutCustomerValidator() => RuleFor(command => command.RefreshToken).NotEmpty();
}