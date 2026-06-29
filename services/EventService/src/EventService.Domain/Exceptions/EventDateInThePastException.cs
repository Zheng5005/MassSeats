namespace EventService.Domain.Exceptions;

public sealed class EventDateInThePastException : DomainException
{
    public EventDateInThePastException(DateTimeOffset date)
        : base($"'{date}' is not a valid date for an event; it must be in the future.") { }
}
