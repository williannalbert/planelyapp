using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Validation;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Organization.Customers.UpdateProfile;

internal sealed class UpdateCustomerProfileHandler(ICustomerRepository customers)
    : ICommandHandler<UpdateCustomerProfileCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        UpdateCustomerProfileCommand command,
        CancellationToken cancellationToken)
    {
        var customer = await customers.GetByIdAsync(command.CustomerId, cancellationToken);

        if (customer is null)
        {
            return Error.NotFound("Customer.NotFound");
        }

        var cpf = command.Cpf.OrNull(Cpf.Create);

        // The CPF the customer already has is not a clash (and saves the query).
        if (cpf is not null
            && !cpf.Equals(customer.Cpf)
            && await customers.ExistsByCpfAsync(customer.CompanyId, cpf, cancellationToken))
        {
            return Error.Conflict("Customer.CpfAlreadyRegistered");
        }

        customer.UpdateProfile(
            command.Name,
            cpf,
            command.BirthDate,
            command.PreferredLanguage.OrNull(Language.Create));

        return Result.Success();
    }
}