using BuildingBlocks.Domain;
using BookingService.Domain.Enums;
using BookingService.Domain.Events;
using BookingService.Domain.Exceptions;

namespace BookingService.Domain.Entities;

/// <summary>
/// Aggregate root that represents a seat reservation for an event.
/// It owns its own lifecycle (Pending → Confirmed / Cancelled / Expired)
/// and raises domain events on every meaningful transition so the outbox
/// can turn them into integration events. State changes go through
/// behavior methods only — no anemic model, no public setters.
/// </summary>
public class Reservation : AggregateRoot
{
    public Guid UserId { get; private set; }
    public Guid EventId { get; private set; }
    public string SeatSection { get; private set; }
    public string SeatRow { get; private set; }
    public int SeatNumber { get; private set; }
    public decimal Price { get; private set; }
    public ReservationStatus Status { get; private set; }
    public Guid? PaymentId { get; private set; }
    public DateTimeOffset ReservedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    // Required by EF Core to materialize entities; not for application code.
    private Reservation()
    {
        SeatSection = null!;
        SeatRow = null!;
    }

    private Reservation(
        Guid id,
        Guid userId,
        Guid eventId,
        string seatSection,
        string seatRow,
        int seatNumber,
        decimal price,
        DateTimeOffset reservedAt,
        DateTimeOffset expiresAt) : base(id)
    {
        UserId = userId;
        EventId = eventId;
        SeatSection = seatSection;
        SeatRow = seatRow;
        SeatNumber = seatNumber;
        Price = price;
        Status = ReservationStatus.Pending;
        ReservedAt = reservedAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// Creates a tentative (Pending) reservation that holds the seat until
    /// <paramref name="holdDuration"/> elapses. The caller (Application)
    /// owns the hold policy; the domain only computes the deadline.
    /// </summary>
    public static Reservation Create(
        Guid userId,
        Guid eventId,
        string seatSection,
        string seatRow,
        int seatNumber,
        decimal price,
        TimeSpan holdDuration)
    {
        if (userId == Guid.Empty)
            throw new DomainValidationException("UserId is required.");
        if (eventId == Guid.Empty)
            throw new DomainValidationException("EventId is required.");
        if (string.IsNullOrWhiteSpace(seatSection))
            throw new DomainValidationException("Seat section is required.");
        if (string.IsNullOrWhiteSpace(seatRow))
            throw new DomainValidationException("Seat row is required.");
        if (seatNumber <= 0)
            throw new DomainValidationException("Seat number must be greater than zero.");
        if (price < 0)
            throw new DomainValidationException("Price cannot be negative.");
        if (holdDuration <= TimeSpan.Zero)
            throw new DomainValidationException("Hold duration must be positive.");

        var now = DateTimeOffset.UtcNow;

        var reservation = new Reservation(
            id: Guid.NewGuid(),
            userId: userId,
            eventId: eventId,
            seatSection: seatSection.Trim(),
            seatRow: seatRow.Trim(),
            seatNumber: seatNumber,
            price: price,
            reservedAt: now,
            expiresAt: now.Add(holdDuration));

        reservation.RaiseDomainEvent(new ReservationCreatedDomainEvent(
            reservation.Id,
            reservation.EventId,
            reservation.UserId,
            reservation.SeatSection,
            reservation.SeatRow,
            reservation.SeatNumber,
            reservation.Price));

        return reservation;
    }

    /// <summary>Confirms the reservation after a successful payment.</summary>
    public void Confirm(Guid paymentId)
    {
        EnsurePending(nameof(Confirm));

        if (IsExpired(DateTimeOffset.UtcNow))
            throw new InvalidReservationStateException(Id, Status, "confirm (hold already elapsed)");

        if (paymentId == Guid.Empty)
            throw new DomainValidationException("PaymentId is required to confirm a reservation.");

        Status = ReservationStatus.Confirmed;
        PaymentId = paymentId;

        RaiseDomainEvent(new ReservationConfirmedDomainEvent(Id, EventId));
    }

    /// <summary>Cancels a still-active (Pending) reservation.</summary>
    public void Cancel()
    {
        EnsurePending(nameof(Cancel));

        Status = ReservationStatus.Cancelled;

        RaiseDomainEvent(new ReservationCancelledDomainEvent(
            Id, EventId, SeatSection, SeatRow, SeatNumber));
    }

    /// <summary>Expires a Pending reservation whose hold has elapsed.</summary>
    public void Expire()
    {
        EnsurePending(nameof(Expire));

        Status = ReservationStatus.Expired;

        RaiseDomainEvent(new ReservationExpiredDomainEvent(
            Id, EventId, SeatSection, SeatRow, SeatNumber));
    }

    /// <summary>True when the hold deadline has passed and it is still Pending.</summary>
    public bool IsExpired(DateTimeOffset asOf) =>
        Status == ReservationStatus.Pending && asOf >= ExpiresAt;

    private void EnsurePending(string action)
    {
        if (Status != ReservationStatus.Pending)
            throw new InvalidReservationStateException(Id, Status, action.ToLowerInvariant());
    }
}
