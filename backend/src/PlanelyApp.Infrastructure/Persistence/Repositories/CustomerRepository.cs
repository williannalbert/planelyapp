using Microsoft.EntityFrameworkCore;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Infrastructure.Persistence.Repositories;

internal sealed class CustomerRepository(PlanelyDbContext context)
    : Repository<Customer>(context), ICustomerRepository
{
    public Task<Customer?> FindByEmailAsync(Guid companyId, Email email, CancellationToken cancellationToken = default) =>
        Context.Set<Customer>().FirstOrDefaultAsync(
            customer => customer.CompanyId == companyId && customer.Email == email,
            cancellationToken);

    public Task<Customer?> FindByPhoneAsync(Guid companyId, Phone phone, CancellationToken cancellationToken = default) =>
        Context.Set<Customer>().FirstOrDefaultAsync(
            customer => customer.CompanyId == companyId && customer.Phone == phone,
            cancellationToken);

    public Task<bool> ExistsByCpfAsync(Guid companyId, Cpf cpf, CancellationToken cancellationToken = default) =>
        Context.Set<Customer>().AnyAsync(
            customer => customer.CompanyId == companyId && customer.Cpf == cpf,
            cancellationToken);
}