namespace EventService.Infrastructure.Messaging;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = null!;
    public string Content { get; private set; } = null!;
    public DateTimeOffset OccurredOn { get; private set; }
    public DateTimeOffset? ProcessedOn { get; private set; }
    public int Attempts { get; private set; }
    public string? Error { get; private set; }

    private OutboxMessage() { }

    public OutboxMessage(Guid id, string type, string content, DateTimeOffset occurredOn)
    {
        Id = id;
        Type = type;
        Content = content;
        OccurredOn = occurredOn;
    }

    public void MarkProcessed(DateTimeOffset processedOn)
    {
        ProcessedOn = processedOn;
        Error = null;
    }

    public void RecordFailure(string error)
    {
        Attempts++;
        Error = error;
    }
}
