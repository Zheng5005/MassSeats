using System.Text.Json;
using BuildingBlocks.Messaging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Messaging.RabbitMQ;

internal interface IEventConsumerRegistration
{
    Type EventType { get; }
    string EventName { get; }

    Task HandleAsync(
        IServiceProvider serviceProvider,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken);
}

internal sealed class EventConsumerRegistration<TEvent> : IEventConsumerRegistration
    where TEvent : IntegrationEvent
{
    public Type EventType => typeof(TEvent);
    public string EventName => typeof(TEvent).Name;

    public async Task HandleAsync(
        IServiceProvider serviceProvider,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        var @event = JsonSerializer.Deserialize<TEvent>(
            body.Span,
            RabbitMqJsonSerializerOptions.Instance)
            ?? throw new JsonException($"Could not deserialize {EventName}.");

        var consumer = serviceProvider.GetRequiredService<IEventConsumer<TEvent>>();
        await consumer.HandleAsync(@event, cancellationToken);
    }
}

internal static class RabbitMqJsonSerializerOptions
{
    public static readonly JsonSerializerOptions Instance = new(JsonSerializerDefaults.Web);
}
