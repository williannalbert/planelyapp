using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanelyApp.Domain.Identity.Sessions;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Customers;

namespace PlanelyApp.Infrastructure.Persistence.Configurations;

internal sealed class CustomerSessionConfiguration : IEntityTypeConfiguration<CustomerSession>
{
    public void Configure(EntityTypeBuilder<CustomerSession> builder)
    {
        builder.HasKey(session => session.Id);

        builder.Property(session => session.CompanyId);   // get-only
        builder.Property(session => session.CustomerId);  // get-only
        builder.Property(session => session.CreatedAtUtc);
        builder.Property(session => session.ExpiresAtUtc);

        // The hash of the current refresh token is also the concurrency token: two requests renewing the same
        // token read the same hash, and only the first UPDATE matches. The second one fails instead of
        // creating a second valid token.
        builder.Property(session => session.RefreshTokenHash)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsConcurrencyToken();

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(session => session.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // The session belongs to a customer, who takes part in it: a real foreign key.
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(session => session.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // The session is found by the hash of the token the customer presents, on every refresh.
        builder.HasIndex(session => session.RefreshTokenHash).IsUnique();
    }
}