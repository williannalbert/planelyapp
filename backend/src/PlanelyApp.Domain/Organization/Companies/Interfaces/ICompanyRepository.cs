using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Domain.Organization.Companies;

public interface ICompanyRepository : IRepository<Company>
{
    Task<bool> ExistsByCnpjAsync(Cnpj cnpj, CancellationToken cancellationToken = default);
}