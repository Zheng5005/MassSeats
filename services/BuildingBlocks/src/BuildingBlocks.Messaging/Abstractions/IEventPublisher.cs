namespace BuildingBlocks.Messaging.Abstractions;

/// <summary>
/// Publishes integration events to the message bus. The concrete
/// implementation (RabbitMQ) lives in Infrastructure / a future
/// BuildingBlocks.Messaging.RabbitMQ project, never here.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IntegrationEvent;
}
