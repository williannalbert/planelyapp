using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanelyApp.Domain.Catalog.ServiceCategories;
using PlanelyApp.Domain.Organization.Companies;

namespace PlanelyApp.Infrastructure.Persistence.Configurations;

internal sealed class ServiceCategoryConfiguration : IEntityTypeConfiguration<ServiceCategory>
{
    public void Configure(EntityTypeBuilder<ServiceCategory> builder)
    {
        builder.HasKey(category => category.Id);

        builder.Property(category => category.CompanyId); // get-only
        builder.Property(category => category.Name).HasMaxLength(100);

        // Organization is a module this one depends on, and the company is the tenant: a real foreign key.
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(category => category.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}