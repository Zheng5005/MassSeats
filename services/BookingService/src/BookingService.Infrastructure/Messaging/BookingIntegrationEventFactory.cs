using BookingService.Domain.Events;
using BuildingBlocks.Domain;
using BuildingBlocks.Messaging;
using BuildingBlocks.Messaging.Contracts;

namespace BookingService.Infrastructure.Messaging;

internal static class BookingIntegrationEventFactory
{
    public static IntegrationEvent Create(IDomainEvent domainEvent) => domainEvent switch
    {
        ReservationCreatedDomainEvent created => new SeatReserved(
            created.ReservationId,
            created.EventId,
            created.UserId,
            created.SeatSection,
            created.SeatRow,
            created.SeatNumber,
            created.Amount)
        {
            OccurredOn = created.OccurredOn
        },
        ReservationConfirmedDomainEvent confirmed => new ReservationConfirmed(
            confirmed.ReservationId,
            confirmed.EventId)
        {
            OccurredOn = confirmed.OccurredOn
        },
        ReservationCancelledDomainEvent cancelled => new ReservationCancelled(
            cancelled.ReservationId,
            cancelled.EventId,
            cancelled.SeatSection,
            cancelled.SeatRow,
            cancelled.SeatNumber)
        {
            OccurredOn = cancelled.OccurredOn
        },
        ReservationExpiredDomainEvent expired => new ReservationExpired(
            expired.ReservationId,
            expired.EventId,
            expired.SeatSection,
            expired.SeatRow,
            expired.SeatNumber)
        {
            OccurredOn = expired.OccurredOn
        },
        _ => throw new InvalidOperationException(
            $"Domain event '{domainEvent.GetType().Name}' has no integration-event mapping.")
    };
}
