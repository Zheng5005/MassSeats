using EventService.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventService.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).HasColumnName("id");
        builder.Property(message => message.Type).HasColumnName("type").IsRequired();
        builder.Property(message => message.Content).HasColumnName("content").HasColumnType("jsonb").IsRequired();
        builder.Property(message => message.OccurredOn).HasColumnName("occurred_on");
        builder.Property(message => message.ProcessedOn).HasColumnName("processed_on");
        builder.Property(message => message.Attempts).HasColumnName("attempts");
        builder.Property(message => message.Error).HasColumnName("error");
        builder.HasIndex(message => new { message.ProcessedOn, message.OccurredOn })
            .HasDatabaseName("ix_outbox_messages_pending");
    }
}
