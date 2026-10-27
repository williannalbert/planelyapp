using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Infrastructure.Persistence.Converters;

namespace PlanelyApp.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(user => user.Id);

        builder.Property(user => user.CompanyId); // get-only; null for the platform administrator
        builder.Property(user => user.IdentityId).HasMaxLength(100); // get-only
        builder.Property(user => user.Name).HasMaxLength(100);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(user => user.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Found on every request, from the "sub" of the token.
        builder.HasIndex(user => user.IdentityId).IsUnique();
        builder.HasIndex(user => new { user.CompanyId, user.Email }).IsUnique();

        // The Domain exposes a read-only view of the list; the list itself is the private field, and that is what
        // is stored: a JSON array in the user's own row.
        builder.Ignore(user => user.AllowedBranchIds);
        builder.Property<List<Guid>>("_allowedBranchIds")
            .HasConversion(new GuidListConverter(), new GuidListComparer())
            .HasColumnType("json")
            .HasColumnName("allowed_branch_ids");
    }
}