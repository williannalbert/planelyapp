using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Application.Abstractions.Validation;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Organization.Customers.Register;

internal sealed class RegisterCustomerHandler(ICustomerRepository customers, ITenantProvider tenantProvider)
    : ICommandHandler<RegisterCustomerCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(RegisterCustomerCommand command, CancellationToken cancellationToken)
    {
        var companyId = tenantProvider.RequireCompanyId();
        var email = command.Email.OrNull(Email.Create);
        var phone = command.Phone.OrNull(Phone.Create);

        // E-mail and phone identify the customer inside the company (RN24). The Domain cannot see the others.
        if (email is not null && await customers.FindByEmailAsync(companyId, email, cancellationToken) is not null)
        {
            return Error.Conflict("Customer.EmailAlreadyRegistered");
        }

        if (phone is not null && await customers.FindByPhoneAsync(companyId, phone, cancellationToken) is not null)
        {
            return Error.Conflict("Customer.PhoneAlreadyRegistered");
        }

        var customer = Customer.Register(
            companyId,
            email,
            phone,
            command.Name,
            command.AcceptsMarketing,
            command.PreferredLanguage.OrNull(Language.Create));

        customers.Add(customer);

        return customer.Id;
    }
}