using BuildingBlocks.Domain;
using BuildingBlocks.Messaging;
using BuildingBlocks.Messaging.Contracts;
using EventService.Domain.Events;

namespace EventService.Infrastructure.Messaging;

internal static class EventIntegrationEventFactory
{
    public static IntegrationEvent Create(IDomainEvent domainEvent) => domainEvent switch
    {
        EventCreatedDomainEvent created => new EventCreated(
            created.EventId,
            created.Title,
            created.VenueId,
            created.CategoryId,
            created.EventDate,
            created.TotalSeats)
        {
            OccurredOn = created.OccurredOn
        },
        EventUpdatedDomainEvent updated => new EventUpdated(
            updated.EventId,
            updated.Title,
            updated.EventDate,
            updated.TotalSeats)
        {
            OccurredOn = updated.OccurredOn
        },
        EventCancelledDomainEvent cancelled => new EventCancelled(cancelled.EventId)
        {
            OccurredOn = cancelled.OccurredOn
        },
        _ => throw new InvalidOperationException(
            $"Domain event '{domainEvent.GetType().Name}' has no integration-event mapping.")
    };
}
