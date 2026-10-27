using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Infrastructure.Persistence.Configurations;

internal sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.HasKey(branch => branch.Id);

        // Get-only: not mapped by convention.
        builder.Property(branch => branch.CompanyId);

        builder.Property(branch => branch.Name).HasMaxLength(100);
        builder.Property(branch => branch.TimeZoneId).HasMaxLength(64);

        // Foreign key without navigation: the database guarantees the company exists,
        // and the aggregates still reference each other only by id.
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(branch => branch.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(branch => new { branch.CompanyId, branch.Name }).IsUnique();

        builder.OwnsOne(branch => branch.Address, address =>
        {
            address.Property(a => a.Street).HasMaxLength(150);
            address.Property(a => a.Number).HasMaxLength(20);
            address.Property(a => a.Complement).HasMaxLength(100);
            address.Property(a => a.District).HasMaxLength(100);
            address.Property(a => a.City).HasMaxLength(100);
            address.Property(a => a.State).HasMaxLength(2).IsFixedLength();
            address.Property(a => a.PostalCode).HasMaxLength(8).IsFixedLength();
        });

        builder.Navigation(branch => branch.Address).IsRequired();

        // Mapped in 3.6, together with the catalog.
        builder.Ignore(branch => branch.OfferingOverrides);

        // A value object has no identity, so each row gets a technical key. The business rule
        // "one entry per weekday" is the unique index.
        builder.OwnsMany(branch => branch.BusinessHours, schedule =>
        {
            schedule.ToTable("branch_schedule");
            schedule.WithOwner().HasForeignKey("BranchId");

            schedule.Property<int>("Id").ValueGeneratedOnAdd();
            schedule.HasKey("Id");
            schedule.HasIndex("BranchId", nameof(DailySchedule.Day)).IsUnique();

            ScheduleMapping.MapSchedule(schedule, "branch_schedule_break", "BranchScheduleId");
        });

        builder.Navigation(branch => branch.BusinessHours).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}