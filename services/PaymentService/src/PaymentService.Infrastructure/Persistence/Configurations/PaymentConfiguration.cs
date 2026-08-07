using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for the Payment aggregate. snake_case column names.
/// The database shape never leaks into the domain.
/// </summary>
public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id");

        builder.Property(p => p.BookingId)
            .HasColumnName("booking_id");

        builder.Property(p => p.StripePaymentIntentId)
            .HasColumnName("stripe_payment_intent_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.ClientSecret)
            .HasColumnName("client_secret")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(p => p.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2);

        builder.Property(p => p.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(p => p.PaymentMethod)
            .HasColumnName("payment_method")
            .HasMaxLength(50);

        builder.Property(p => p.FailureReason)
            .HasColumnName("failure_reason")
            .HasMaxLength(500);

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at");

        // One payment per booking: DB-level backstop for the idempotency
        // check in InitiateAsync (guards the check-then-insert race).
        builder.HasIndex(p => p.BookingId)
            .IsUnique();

        // The webhook looks a payment up by this id, so it must be unique.
        builder.HasIndex(p => p.StripePaymentIntentId)
            .IsUnique();

        builder.Ignore(p => p.DomainEvents);
    }
}
