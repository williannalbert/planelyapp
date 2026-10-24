namespace PlanelyApp.Infrastructure.Persistence.Interceptors;

/// <summary>Names of the shadow properties every aggregate root gets. Columns: created_at and updated_at.</summary>
public static class AuditProperties
{
    public const string CreatedAt = "CreatedAt";
    public const string UpdatedAt = "UpdatedAt";
}