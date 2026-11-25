using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Infrastructure.Development;

internal enum DevelopmentCompanySeedResult
{
    Created,
    AlreadyThere,
    CnpjTaken,
}

/// <summary>
/// Creates the company of development and its administrator, together or not at all. Neither the company nor the user is a
/// tenant entity, so no company has to be set to save them. The administrator is what tells whether it already ran.
/// </summary>
internal sealed class DevelopmentCompanySeed(
    ICompanyRepository companies,
    IUserRepository users,
    IUnitOfWork unitOfWork)
{
    public async Task<DevelopmentCompanySeedResult> EnsureAsync(
        DevelopmentCompanyOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (await users.GetByIdentityIdAsync(options.AdminIdentityId, cancellationToken) is not null)
        {
            return DevelopmentCompanySeedResult.AlreadyThere;
        }

        var cnpj = Cnpj.Create(options.Cnpj);

        if (await companies.ExistsByCnpjAsync(cnpj, cancellationToken))
        {
            return DevelopmentCompanySeedResult.CnpjTaken;
        }

        var company = Company.Create(
            options.TradeName,
            options.LegalName,
            cnpj,
            Email.Create(options.Email),
            Phone.Create(options.Phone),
            Language.Create(options.DefaultLanguage));

        var admin = User.CreateForCompany(
            company.Id,
            options.AdminIdentityId,
            options.AdminName,
            Email.Create(options.AdminEmail),
            UserRole.CompanyAdmin);

        // A new user has no branch at all until it is told otherwise.
        admin.SetBranchAccess(true);

        companies.Add(company);
        users.Add(admin);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return DevelopmentCompanySeedResult.Created;
    }
}