using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanelyApp.Domain.Organization.Companies;

namespace PlanelyApp.Infrastructure.Persistence.Configurations;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    // Lengths repeat the limits the Domain enforces; a test saves the longest allowed values.
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.HasKey(company => company.Id);

        builder.Property(company => company.TradeName).HasMaxLength(150);
        builder.Property(company => company.LegalName).HasMaxLength(200);

        // Get-only: not mapped by convention. Converter and length come from ConfigureConventions.
        builder.Property(company => company.Cnpj);

        builder.HasIndex(company => company.Cnpj).IsUnique();

        // Same table, columns booking_policy_*.
        builder.OwnsOne(company => company.BookingPolicy, policy =>
        {
            policy.Property(p => p.MinimumNoticeHours);
            policy.Property(p => p.MaximumWindowDays);
            policy.Property(p => p.CancellationDeadlineHours);
            policy.Property(p => p.IntervalBetweenAppointmentsMinutes);
            policy.Property(p => p.TravelTimeBetweenBranchesMinutes);
            policy.Property(p => p.RequiresConfirmation);
        });

        builder.Navigation(company => company.BookingPolicy).IsRequired();
    }
}