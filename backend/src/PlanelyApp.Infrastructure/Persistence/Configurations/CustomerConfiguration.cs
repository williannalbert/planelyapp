using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Customers;

namespace PlanelyApp.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(customer => customer.Id);

        builder.Property(customer => customer.CompanyId); // get-only
        builder.Property(customer => customer.Name).HasMaxLength(150);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(customer => customer.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Login by code (RN24) identifies the customer by phone or e-mail inside the company.
        // MySQL accepts many NULLs in a unique index, so a customer without e-mail does not conflict.
        builder.HasIndex(customer => new { customer.CompanyId, customer.Phone }).IsUnique();
        builder.HasIndex(customer => new { customer.CompanyId, customer.Email }).IsUnique();
        builder.HasIndex(customer => new { customer.CompanyId, customer.Cpf }).IsUnique();
    }
}