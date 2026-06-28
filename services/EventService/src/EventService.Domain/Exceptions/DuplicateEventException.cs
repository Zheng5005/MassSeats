namespace EventService.Domain.Exceptions;

public sealed class DuplicateEventException : DomainException
{
    public DuplicateEventException(string title)
          : base($"An event with title '{title}' already exists.")
}
