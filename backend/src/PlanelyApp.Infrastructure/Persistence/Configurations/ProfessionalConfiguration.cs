using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Infrastructure.Persistence.Converters;

namespace PlanelyApp.Infrastructure.Persistence.Configurations;

internal sealed class ProfessionalConfiguration : IEntityTypeConfiguration<Professional>
{
    public void Configure(EntityTypeBuilder<Professional> builder)
    {
        builder.HasKey(professional => professional.Id);

        builder.Property(professional => professional.CompanyId); // get-only
        builder.Property(professional => professional.Name).HasMaxLength(100);
        builder.Property(professional => professional.Specialty).HasMaxLength(100);

        // RN27: the Domain raises the version on every change to the agenda. EF puts the version it read in the
        // WHERE of the UPDATE, so of two simultaneous writes only the first one matches a row.
        builder.Property(professional => professional.ScheduleVersion).IsConcurrencyToken();

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(professional => professional.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Same module, so a real foreign key. Null when the professional does not use the system.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(professional => professional.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(professional => professional.UserId).IsUnique();

        // The working day of the professional at a branch. The DailySchedule shares this table
        // (day, start_time, end_time); only its breaks have a table of their own.
        builder.OwnsMany(professional => professional.WorkingHours, hours =>
        {
            hours.ToTable("working_hours", table =>
                table.HasCheckConstraint("ck_working_hours_range", "end_time > start_time"));
            hours.WithOwner().HasForeignKey("ProfessionalId");

            hours.Property<int>("Id").ValueGeneratedOnAdd();
            hours.HasKey("Id");

            hours.Property(h => h.BranchId); // get-only
            hours.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(h => h.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            hours.OwnsOne(h => h.Schedule, schedule =>
                ScheduleMapping.MapSchedule(schedule, "working_hours_break", "WorkingHoursId"));
            hours.Navigation(h => h.Schedule).IsRequired();
        });

        // Time off is an entity with an identity (the Domain removes it by id), so it keeps the Guid.
        builder.OwnsMany(professional => professional.TimeOffs, timeOff =>
        {
            timeOff.ToTable("time_off", table =>
                table.HasCheckConstraint("ck_time_off_period", "ends_at > starts_at"));
            timeOff.WithOwner().HasForeignKey("ProfessionalId");

            timeOff.HasKey(t => t.Id);
            timeOff.Property(t => t.Reason).HasMaxLength(200);

            timeOff.OwnsOne(t => t.Range, range =>
            {
                range.Property(r => r.Start).HasColumnName("starts_at");
                range.Property(r => r.End).HasColumnName("ends_at");
            });
            timeOff.Navigation(t => t.Range).IsRequired();
        });

        // No foreign key to the service: it belongs to the Catalog module.
        builder.OwnsMany(professional => professional.Offerings, offering =>
        {
            offering.ToTable("professional_offering");
            offering.WithOwner().HasForeignKey("ProfessionalId");

            offering.Property<int>("Id").ValueGeneratedOnAdd();
            offering.HasKey("Id");

            offering.Property(o => o.ServiceOfferingId); // get-only
            offering.HasIndex("ProfessionalId", nameof(ProfessionalOffering.ServiceOfferingId)).IsUnique();

            offering.Property(o => o.CustomDuration)
                .HasConversion(new TimeSpanMinutesConverter())
                .HasColumnName("custom_duration_minutes");
        });

        builder.Navigation(professional => professional.WorkingHours).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(professional => professional.TimeOffs).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(professional => professional.Offerings).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}