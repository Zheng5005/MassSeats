namespace BuildingBlocks.Messaging;

/// <summary>
/// Base for every message that travels between services over the bus.
/// <para>
/// <see cref="Id"/> is the unique message id used for idempotency
/// (inbox pattern) and de-duplication. <see cref="OccurredOn"/> is when
/// the source event happened. Contracts are plain data — NO behavior,
/// NO business logic (golden rule: only share contracts and pure tech).
/// </para>
/// </summary>
public abstract record IntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredOn { get; init; } = DateTimeOffset.UtcNow;
}
