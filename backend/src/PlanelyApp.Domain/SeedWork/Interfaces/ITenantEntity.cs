namespace PlanelyApp.Domain.SeedWork;

/// <summary>Marks an aggregate root that belongs to a company (tenant).</summary>
public interface ITenantEntity
{
    Guid CompanyId { get; }
}