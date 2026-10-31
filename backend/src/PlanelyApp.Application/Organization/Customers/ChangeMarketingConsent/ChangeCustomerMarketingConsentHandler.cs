using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Customers;

namespace PlanelyApp.Application.Organization.Customers.ChangeMarketingConsent;

internal sealed class ChangeCustomerMarketingConsentHandler(ICustomerRepository customers)
    : ICommandHandler<ChangeCustomerMarketingConsentCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        ChangeCustomerMarketingConsentCommand command,
        CancellationToken cancellationToken)
    {
        var customer = await customers.GetByIdAsync(command.CustomerId, cancellationToken);

        if (customer is null)
        {
            return Error.NotFound("Customer.NotFound");
        }

        customer.ChangeMarketingConsent(command.AcceptsMarketing);

        return Result.Success();
    }
}