using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Infrastructure.Persistence.Converters;

namespace PlanelyApp.Infrastructure.Persistence.Configurations;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.HasKey(appointment => appointment.Id);

        // Get-only: not mapped by convention.
        builder.Property(appointment => appointment.CompanyId);
        builder.Property(appointment => appointment.BranchId);
        builder.Property(appointment => appointment.CustomerId);
        builder.Property(appointment => appointment.ProfessionalId);

        builder.Property(appointment => appointment.Notes).HasMaxLength(1000);

        // Status is stored as text by the enum convention; TotalPrice (Money?) has its converter and decimal(12,2)
        // from ConfigureConventions, and is null while a price is pending.

        // Who takes part in the appointment: real foreign keys. Nothing here is ever deleted, only deactivated.
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(appointment => appointment.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(appointment => appointment.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(appointment => appointment.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Professional>()
            .WithMany()
            .HasForeignKey(appointment => appointment.ProfessionalId)
            .OnDelete(DeleteBehavior.Restrict);

        // The indexes on (professional, period), (branch, start), (customer, start) and (status, start) are not here:
        // EF Core 10 cannot index an owned type together with its owner. They are written by hand in the migration.

        // Same table, columns starts_at and ends_at (UTC).
        builder.OwnsOne(appointment => appointment.Period, period =>
        {
            period.Property(p => p.Start).HasColumnName("starts_at");
            period.Property(p => p.End).HasColumnName("ends_at");
        });

        builder.Navigation(appointment => appointment.Period).IsRequired();

        // The services of the appointment, with a copy of name, duration and prices at booking time.
        // They have an identity (the Domain removes one by id), so the key is the Guid of the Domain.
        // No foreign key to the service or to the promotion: the item is a snapshot and must not depend on them.
        builder.OwnsMany(appointment => appointment.Items, item =>
        {
            item.ToTable("appointment_item");
            item.WithOwner().HasForeignKey("AppointmentId");
            item.HasKey(i => i.Id);

            item.Property(i => i.ServiceOfferingId);              // get-only
            item.Property(i => i.ServiceName).HasMaxLength(150);  // get-only
            item.Property(i => i.IsOnRequest);                    // get-only

            item.Property(i => i.Duration)
                .HasConversion(new TimeSpanMinutesConverter())
                .HasColumnName("duration_minutes");

            // ListPrice, Discount and FinalPrice are Money: converter and decimal(12,2) come from ConfigureConventions.
        });

        // RN08: one line per status change. ChangedBy is a user or a customer, so it cannot be a foreign key.
        builder.OwnsMany(appointment => appointment.History, change =>
        {
            change.ToTable("appointment_status_change");
            change.WithOwner().HasForeignKey("AppointmentId");
            change.HasKey(c => c.Id);

            // Get-only properties are mapped explicitly. "from" is a reserved word in SQL, hence the names.
            change.Property(c => c.From).HasColumnName("from_status");
            change.Property(c => c.To).HasColumnName("to_status");
            change.Property(c => c.OccurredAtUtc).HasColumnName("occurred_at");
            change.Property(c => c.ChangedBy);
            change.Property(c => c.Reason).HasMaxLength(500);
        });

        builder.Navigation(appointment => appointment.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(appointment => appointment.History).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}