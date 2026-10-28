using FluentValidation;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Abstractions.Validation;

public static class DomainRuleExtensions
{
    /// <summary>
    /// The text must be accepted by the factory of a Domain value object (<c>Email.Create</c>, <c>Cnpj.Create</c>...).
    /// The rule stays written in the Domain only. An empty value passes here: say so with <c>NotEmpty()</c>.
    /// </summary>
    public static IRuleBuilderOptions<T, string> AcceptedBy<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        Func<string?, object> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        return ruleBuilder
            .Must(value => string.IsNullOrWhiteSpace(value) || IsAccepted(factory, value))
            .WithErrorCode(ValidationCodes.Invalid);
    }

    private static bool IsAccepted(Func<string?, object> factory, string value)
    {
        try
        {
            factory(value);

            return true;
        }
        catch (DomainException)
        {
            return false;
        }
    }
}