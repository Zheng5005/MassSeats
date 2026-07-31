using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentService.Infrastructure.Messaging;

namespace PaymentService.Infrastructure.Persistence.Configurations;

public sealed class ProcessedStripeEventConfiguration : IEntityTypeConfiguration<ProcessedStripeEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedStripeEvent> builder)
    {
        builder.ToTable("processed_stripe_events");

        builder.HasKey(processedEvent => processedEvent.StripeEventId);

        builder.Property(processedEvent => processedEvent.StripeEventId)
            .HasColumnName("stripe_event_id")
            .IsRequired();

        builder.Property(processedEvent => processedEvent.ProcessedOn)
            .HasColumnName("processed_on");
    }
}
