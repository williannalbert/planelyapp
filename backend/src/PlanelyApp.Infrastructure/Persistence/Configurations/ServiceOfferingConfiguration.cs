using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanelyApp.Domain.Catalog.ServiceCategories;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Infrastructure.Persistence.Converters;

namespace PlanelyApp.Infrastructure.Persistence.Configurations;

internal sealed class ServiceOfferingConfiguration : IEntityTypeConfiguration<ServiceOffering>
{
    public void Configure(EntityTypeBuilder<ServiceOffering> builder)
    {
        builder.HasKey(service => service.Id);

        builder.Property(service => service.CompanyId); // get-only
        builder.Property(service => service.Name).HasMaxLength(150);
        builder.Property(service => service.Description).HasMaxLength(2000);

        builder.Property(service => service.StandardDuration)
            .HasConversion(new TimeSpanMinutesConverter())
            .HasColumnName("standard_duration_minutes");

        // BasePrice (Money?) has its converter and decimal(12,2) from ConfigureConventions; null for on-request services.
        // PricingType is stored as text by the enum convention.

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(service => service.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Same module, so a real foreign key. Optional: a service may have no category.
        builder.HasOne<ServiceCategory>()
            .WithMany()
            .HasForeignKey(service => service.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // The spreadsheet import finds existing services by name.
        builder.HasIndex(service => new { service.CompanyId, service.Name }).IsUnique();
    }
}