using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Infrastructure.Persistence.Converters;

namespace PlanelyApp.Infrastructure.Persistence.Configurations;

internal sealed class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.HasKey(promotion => promotion.Id);

        builder.Property(promotion => promotion.CompanyId); // get-only
        builder.Property(promotion => promotion.Kind);      // get-only
        builder.Property(promotion => promotion.Name).HasMaxLength(100);
        builder.Property(promotion => promotion.Description).HasMaxLength(1000);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(promotion => promotion.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // The validity lives in an owned type, and EF cannot index an owned type together with its owner,
        // so the query narrows by company and active flag and the Domain checks the period.
        builder.HasIndex(promotion => new { promotion.CompanyId, promotion.IsActive });

        // Same table, columns discount_type and discount_value.
        builder.OwnsOne(promotion => promotion.Discount, discount =>
        {
            discount.Property(d => d.Type);
            discount.Property(d => d.Value).HasPrecision(12, 2);
        });

        builder.Navigation(promotion => promotion.Discount).IsRequired();

        // Same table, columns valid_from and valid_until (calendar days, both inclusive).
        builder.OwnsOne(promotion => promotion.Validity, validity =>
        {
            validity.Property(v => v.Start).HasColumnName("valid_from");
            validity.Property(v => v.End).HasColumnName("valid_until");
        });

        builder.Navigation(promotion => promotion.Validity).IsRequired();

        // The Domain exposes read-only views; the private lists are what is stored, in the promotion's own row.
        builder.Ignore(promotion => promotion.BranchIds);
        builder.Ignore(promotion => promotion.ServiceIds);
        builder.Ignore(promotion => promotion.Weekdays);

        builder.Property<List<Guid>>("_branchIds")
            .HasConversion(new GuidListConverter(), new ListComparer<Guid>())
            .HasColumnType("json")
            .HasColumnName("branch_ids");

        builder.Property<List<Guid>>("_serviceIds")
            .HasConversion(new GuidListConverter(), new ListComparer<Guid>())
            .HasColumnType("json")
            .HasColumnName("service_ids");

        builder.Property<List<DayOfWeek>>("_weekdays")
            .HasConversion(new WeekdaysConverter(), new ListComparer<DayOfWeek>())
            .HasColumnName("days_of_week_mask");
    }
}