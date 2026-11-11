using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Validation;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Organization.Customers.UpdateContact;

internal sealed class UpdateCustomerContactHandler(ICustomerRepository customers)
    : ICommandHandler<UpdateCustomerContactCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        UpdateCustomerContactCommand command,
        CancellationToken cancellationToken)
    {
        var customer = await customers.GetByIdAsync(command.CustomerId, cancellationToken);

        if (customer is null)
        {
            return Error.NotFound("Customer.NotFound");
        }

        var email = command.Email.OrNull(Email.Create);
        var phone = command.Phone.OrNull(Phone.Create);

        // Finding the customer itself is not a clash: keeping a contact must not fail.
        if (email is not null
            && await customers.FindByEmailAsync(customer.CompanyId, email, cancellationToken) is { } sameEmail
            && sameEmail.Id != customer.Id)
        {
            return Error.Conflict("Customer.EmailAlreadyRegistered");
        }

        if (phone is not null
            && await customers.FindByPhoneAsync(customer.CompanyId, phone, cancellationToken) is { } samePhone
            && samePhone.Id != customer.Id)
        {
            return Error.Conflict("Customer.PhoneAlreadyRegistered");
        }

        customer.UpdateContact(email, phone);

        return Result.Success();
    }
}