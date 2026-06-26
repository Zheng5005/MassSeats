namespace UserService.Domain.Exceptions;

public sealed class DuplicateEmailException : DomainException
{
    public DuplicateEmailException(string email)
        : base($"A user with email '{email}' already exists.") { }
}
