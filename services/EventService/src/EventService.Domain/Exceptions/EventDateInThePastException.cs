namespace EventService.Domain.Exceptions;

public sealed class EventDateInThePastEventException : DomainException
{
    public DuplicateEventException(DateTimeOffset date)
          : base($"'{date}' is not a valid date for an Event")
}
