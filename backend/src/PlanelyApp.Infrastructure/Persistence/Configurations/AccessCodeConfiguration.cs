using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanelyApp.Domain.Identity.AccessCodes;
using PlanelyApp.Domain.Organization.Companies;

namespace PlanelyApp.Infrastructure.Persistence.Configurations;

internal sealed class AccessCodeConfiguration : IEntityTypeConfiguration<AccessCode>
{
    public void Configure(EntityTypeBuilder<AccessCode> builder)
    {
        builder.HasKey(code => code.Id);

        // Get-only: not mapped by convention.
        builder.Property(code => code.CompanyId);
        builder.Property(code => code.Channel);
        builder.Property(code => code.Destination).HasMaxLength(254); // the longest e-mail; a phone is shorter
        builder.Property(code => code.CreatedAtUtc);
        builder.Property(code => code.ExpiresAtUtc);

        // SHA-256 in lowercase hex: always 64 characters.
        builder.Property(code => code.CodeHash).HasMaxLength(64).IsFixedLength();

        // The state of the code is its concurrency token. Without it, 100 parallel guesses would all read
        // "0 attempts" and each save "1 attempt", defeating the limit of 5; and the right code could be used twice.
        // With it, the UPDATE only matches if nobody changed attempts or use in the meantime.
        builder.Property(code => code.Attempts).IsConcurrencyToken();
        builder.Property(code => code.UsedAtUtc).IsConcurrencyToken();

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(code => code.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Finds the code the customer is typing and counts the codes sent to a destination (RN22).
        builder.HasIndex(code => new { code.CompanyId, code.Destination, code.CreatedAtUtc });
    }
}