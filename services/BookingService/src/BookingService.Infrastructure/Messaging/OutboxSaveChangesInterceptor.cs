using System.Text.Json;
using BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BookingService.Infrastructure.Messaging;

public sealed class OutboxSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CaptureDomainEvents(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CaptureDomainEvents(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void CaptureDomainEvents(DbContext? context)
    {
        if (context is null)
            return;

        var aggregates = context.ChangeTracker
            .Entries<AggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToArray();

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                var integrationEvent = BookingIntegrationEventFactory.Create(domainEvent);
                context.Set<OutboxMessage>().Add(new OutboxMessage(
                    integrationEvent.Id,
                    integrationEvent.GetType().Name,
                    JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), SerializerOptions),
                    integrationEvent.OccurredOn));
            }

            aggregate.ClearDomainEvents();
        }
    }
}
