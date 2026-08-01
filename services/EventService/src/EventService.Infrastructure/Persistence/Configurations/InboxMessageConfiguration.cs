using EventService.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventService.Infrastructure.Persistence.Configurations;

public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");
        builder.HasKey(message => message.MessageId);
        builder.Property(message => message.MessageId).HasColumnName("message_id");
        builder.Property(message => message.Type).HasColumnName("type").IsRequired();
        builder.Property(message => message.ProcessedOn).HasColumnName("processed_on");
    }
}
