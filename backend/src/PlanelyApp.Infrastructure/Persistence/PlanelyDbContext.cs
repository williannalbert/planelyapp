using Microsoft.EntityFrameworkCore;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Infrastructure.Persistence;

/// <summary>
/// The database session. It is also the unit of work: the repositories only add and load, and the use case
/// saves everything at once with SaveChangesAsync, which this class inherits from DbContext.
/// </summary>
public sealed class PlanelyDbContext(DbContextOptions<PlanelyDbContext> options) : DbContext(options), IUnitOfWork
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Each aggregate gets its own IEntityTypeConfiguration class (from 3.4), found here by scanning.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AssemblyReference).Assembly);
    }
}