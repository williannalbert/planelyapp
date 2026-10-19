namespace PlanelyApp.Domain.SeedWork;

/// <summary>Tells which company the current operation runs for. Implemented in the infrastructure/API.</summary>
public interface ITenantProvider
{
    Guid? CompanyId { get; }
}