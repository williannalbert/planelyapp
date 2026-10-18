using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Domain.Organization.Companies;

public interface ICompanyRepository
{
    Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByCnpjAsync(Cnpj cnpj, CancellationToken cancellationToken = default);

    void Add(Company company);
}