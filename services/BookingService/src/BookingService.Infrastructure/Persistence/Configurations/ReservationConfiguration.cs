using BookingService.Domain.Entities;
using BookingService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingService.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for the Reservation aggregate. This is the ONLY place
/// where the database shape (table/column names, snake_case, the seat
/// unique constraint) is allowed to live — it never leaks into the domain.
/// </summary>
public sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("reservations");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id");

        // Cross-service references (User, Event live in other databases):
        // logical Guids only, NO physical foreign keys. Database-per-service.
        builder.Property(r => r.UserId)
            .HasColumnName("user_id");

        builder.Property(r => r.EventId)
            .HasColumnName("event_id");

        builder.Property(r => r.SeatSection)
            .HasColumnName("seat_section")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.SeatRow)
            .HasColumnName("seat_row")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.SeatNumber)
            .HasColumnName("seat_number");

        builder.Property(r => r.Price)
            .HasColumnName("price")
            .HasPrecision(18, 2);

        // Stored as text (e.g. 'Pending') instead of an int: readable in the
        // DB and robust against enum reordering — the partial index below
        // depends on these exact values.
        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.PaymentId)
            .HasColumnName("payment_id");

        builder.Property(r => r.ReservedAt)
            .HasColumnName("reserved_at");

        builder.Property(r => r.ExpiresAt)
            .HasColumnName("expires_at");

        // The heart of concurrency control. A PARTIAL unique index: a seat
        // can only be held once while a reservation is ACTIVE (Pending or
        // Confirmed). Once it is Cancelled or Expired the row stays but no
        // longer occupies the slot, so the seat can be reserved again.
        // The DB rejects the second concurrent insert with error 23505,
        // which the repository turns into SeatAlreadyReservedException.
        builder.HasIndex(r => new { r.EventId, r.SeatSection, r.SeatRow, r.SeatNumber })
            .IsUnique()
            .HasDatabaseName("ux_reservations_active_seat")
            .HasFilter("status IN ('Pending', 'Confirmed')");

        // In-memory domain events are not persisted.
        builder.Ignore(r => r.DomainEvents);
    }
}
