using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Customers;

namespace PlanelyApp.Application.Organization.Customers.Get;

internal sealed class GetCustomerHandler(ICustomerRepository customers)
    : IQueryHandler<GetCustomerQuery, CustomerDto>
{
    public async Task<Result<CustomerDto>> HandleAsync(GetCustomerQuery query, CancellationToken cancellationToken)
    {
        var customer = await customers.GetByIdAsync(query.CustomerId, cancellationToken);

        if (customer is null)
        {
            return Error.NotFound("Customer.NotFound");
        }

        return new CustomerDto(
            customer.Id,
            customer.Name,
            customer.Email?.Value,
            customer.Phone?.Value,
            customer.Cpf?.Formatted,
            customer.BirthDate,
            customer.LastAccessAt,
            customer.AcceptsMarketing,
            customer.PreferredLanguage?.Code);
    }
}