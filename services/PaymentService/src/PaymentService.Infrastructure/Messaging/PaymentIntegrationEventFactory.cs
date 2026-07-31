using BuildingBlocks.Domain;
using BuildingBlocks.Messaging;
using BuildingBlocks.Messaging.Contracts;
using PaymentService.Domain.Events;

namespace PaymentService.Infrastructure.Messaging;

internal static class PaymentIntegrationEventFactory
{
    public static IntegrationEvent? Create(IDomainEvent domainEvent) => domainEvent switch
    {
        PaymentSucceededDomainEvent succeeded => new PaymentSucceeded(
            succeeded.PaymentId,
            succeeded.BookingId)
        {
            OccurredOn = succeeded.OccurredOn
        },
        PaymentFailedDomainEvent failed => new PaymentFailed(
            failed.PaymentId,
            failed.BookingId,
            failed.Reason)
        {
            OccurredOn = failed.OccurredOn
        },
        PaymentInitiatedDomainEvent => null,
        _ => throw new InvalidOperationException(
            $"Domain event '{domainEvent.GetType().Name}' has no integration-event mapping.")
    };
}
