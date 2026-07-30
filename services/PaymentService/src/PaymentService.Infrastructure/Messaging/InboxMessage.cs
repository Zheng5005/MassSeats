namespace PaymentService.Infrastructure.Messaging;

public sealed class InboxMessage
{
    public Guid MessageId { get; private set; }
    public string Type { get; private set; } = null!;
    public DateTimeOffset ProcessedOn { get; private set; }

    private InboxMessage() { }

    public InboxMessage(Guid messageId, string type, DateTimeOffset processedOn)
    {
        MessageId = messageId;
        Type = type;
        ProcessedOn = processedOn;
    }
}
