using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanelyApp.Domain.Imports;
using PlanelyApp.Domain.Organization.Companies;

namespace PlanelyApp.Infrastructure.Persistence.Configurations;

internal sealed class DataImportConfiguration : IEntityTypeConfiguration<DataImport>
{
    public void Configure(EntityTypeBuilder<DataImport> builder)
    {
        builder.HasKey(import => import.Id);

        // Get-only: not mapped by convention.
        builder.Property(import => import.CompanyId);
        builder.Property(import => import.Type);
        builder.Property(import => import.FileName).HasMaxLength(255);
        builder.Property(import => import.StoragePath).HasMaxLength(500);
        builder.Property(import => import.RequestedBy); // a user, but no foreign key
        builder.Property(import => import.CreatedAtUtc);

        builder.Property(import => import.FailureReason).HasMaxLength(500);

        // Only one job can take an import from Pending to Processing: the second one reads "Pending" too,
        // but its UPDATE no longer matches. The row counters change on every row and are not tokens.
        builder.Property(import => import.Status).IsConcurrencyToken();

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(import => import.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // The Domain keeps the first 1000 errors. The job adds them one by one, so they are rows of their own and
        // saving a new error inserts a line instead of rewriting the others.
        builder.OwnsMany(import => import.Errors, error =>
        {
            error.ToTable("data_import_error");
            error.WithOwner().HasForeignKey("DataImportId");

            error.Property<int>("Id").ValueGeneratedOnAdd();
            error.HasKey("Id");

            // "row" is a reserved word in MySQL; this is the line as the user sees it in the spreadsheet.
            error.Property(e => e.Row).HasColumnName("line_number");
            error.Property(e => e.Reason).HasMaxLength(500);
        });

        builder.Navigation(import => import.Errors).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}