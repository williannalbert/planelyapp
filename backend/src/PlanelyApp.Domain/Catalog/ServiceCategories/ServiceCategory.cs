using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.Catalog.ServiceCategories;

/// <summary>Groups the services of a company (e.g. "Hair", "Tattoo").</summary>
public sealed class ServiceCategory : AggregateRoot, ITenantEntity
{
    private const int NameMaxLength = 100;

    private ServiceCategory(Guid companyId, string name)
    {
        CompanyId = companyId;
        Name = name;
    }

    public Guid CompanyId { get; }

    public string Name { get; private set; }

    public static ServiceCategory Create(Guid companyId, string? name)
    {
        if (companyId == Guid.Empty)
        {
            throw new DomainException("ServiceCategory.CompanyRequired");
        }

        return new ServiceCategory(
            companyId,
            Guard.RequiredText(name, NameMaxLength, "ServiceCategory.NameInvalid"));
    }

    public void Rename(string? name) =>
        Name = Guard.RequiredText(name, NameMaxLength, "ServiceCategory.NameInvalid");
}