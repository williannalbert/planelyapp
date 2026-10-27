using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanelyApp.Infrastructure.Messaging.Outbox;

namespace PlanelyApp.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        // The only plural table name: "outbox_messages" is how the pattern is known everywhere.
        builder.ToTable("outbox_messages");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Type).HasMaxLength(OutboxMessage.MaxTypeLength);
        builder.Property(message => message.Version);
        builder.Property(message => message.CompanyId); // no foreign key: the row is the record of a fact that already happened
        builder.Property(message => message.Payload).HasColumnType("json");
        builder.Property(message => message.OccurredAtUtc);
        builder.Property(message => message.ProcessedAtUtc);
        builder.Property(message => message.Attempts);
        builder.Property(message => message.Error).HasMaxLength(OutboxMessage.MaxErrorLength);

        // The publisher asks for the pending ones (processed_at_utc IS NULL) in order of occurrence.
        builder.HasIndex(message => new { message.ProcessedAtUtc, message.OccurredAtUtc });
    }
}