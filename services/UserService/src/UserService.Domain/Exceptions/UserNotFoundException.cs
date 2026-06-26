namespace UserService.Domain.Exceptions;

public sealed class UserNotFoundException : DomainException
{
    public UserNotFoundException(Guid id)
        : base($"User with id '{id}' was not found.") { }
}
