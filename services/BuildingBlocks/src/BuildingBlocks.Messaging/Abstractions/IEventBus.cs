namespace BuildingBlocks.Messaging.Abstractions;

/// <summary>
/// The message bus abstraction: publishes events and wires up
/// subscriptions so incoming messages of a given type are routed to
/// the registered <see cref="IEventConsumer{TEvent}"/>. The RabbitMQ
/// concretion is added in the messaging integration phase.
/// </summary>
public interface IEventBus : IEventPublisher
{
    Task SubscribeAsync<TEvent>(CancellationToken ct = default)
        where TEvent : IntegrationEvent;
}
