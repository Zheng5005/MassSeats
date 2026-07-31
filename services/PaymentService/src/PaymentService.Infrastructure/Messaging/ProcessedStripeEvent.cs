namespace PaymentService.Infrastructure.Messaging;

public sealed class ProcessedStripeEvent
{
    public string StripeEventId { get; private set; } = null!;
    public DateTimeOffset ProcessedOn { get; private set; }

    private ProcessedStripeEvent() { }

    public ProcessedStripeEvent(string stripeEventId, DateTimeOffset processedOn)
    {
        StripeEventId = stripeEventId;
        ProcessedOn = processedOn;
    }
}
