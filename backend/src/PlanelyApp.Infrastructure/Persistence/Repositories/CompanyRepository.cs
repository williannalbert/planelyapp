using Microsoft.EntityFrameworkCore;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Infrastructure.Persistence.Repositories;

internal sealed class CompanyRepository(PlanelyDbContext context)
    : Repository<Company>(context), ICompanyRepository
{
    public Task<bool> ExistsByCnpjAsync(Cnpj cnpj, CancellationToken cancellationToken = default) =>
        Context.Set<Company>().AnyAsync(company => company.Cnpj == cnpj, cancellationToken);
}