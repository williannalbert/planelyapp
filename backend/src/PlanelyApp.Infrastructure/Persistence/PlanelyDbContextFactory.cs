using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PlanelyApp.Infrastructure.Tenancy;

namespace PlanelyApp.Infrastructure.Persistence;

/// <summary>Used only by the EF Core tools (migrations add, database update). Never at runtime.</summary>
public sealed class PlanelyDbContextFactory : IDesignTimeDbContextFactory<PlanelyDbContext>
{
    public PlanelyDbContext CreateDbContext(string[] args)
    {
        // "migrations add" does not open a connection. "database update" receives the real one with --connection.
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Server=localhost;Database=planelyapp";

        var options = new DbContextOptionsBuilder<PlanelyDbContext>()
            .UseMySql(connectionString, DatabaseServer.Version)
            .Options;

        return new PlanelyDbContext(options, new TenantContext());
    }
}