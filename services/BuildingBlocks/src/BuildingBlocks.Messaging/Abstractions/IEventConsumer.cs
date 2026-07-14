namespace BuildingBlocks.Messaging.Abstractions;

/// <summary>
/// Handles a specific integration event type. Each service implements
/// one consumer per event it cares about; the bus dispatches incoming
/// messages to the matching consumer.
/// </summary>
public interface IEventConsumer<in TEvent>
    where TEvent : IntegrationEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}
