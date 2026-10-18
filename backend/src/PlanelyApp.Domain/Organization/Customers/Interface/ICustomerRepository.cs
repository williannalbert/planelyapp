using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Domain.Organization.Customers;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    // Phone, e-mail and CPF are only unique inside a company, so the company is part of the lookup.
    Task<Customer?> FindByEmailAsync(Guid companyId, Email email, CancellationToken cancellationToken = default);

    Task<Customer?> FindByPhoneAsync(Guid companyId, Phone phone, CancellationToken cancellationToken = default);

    Task<bool> ExistsByCpfAsync(Guid companyId, Cpf cpf, CancellationToken cancellationToken = default);

    void Add(Customer customer);
}