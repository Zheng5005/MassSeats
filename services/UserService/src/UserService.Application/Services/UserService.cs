using UserService.Application.DTOs;
using UserService.Application.Interfaces;
using UserService.Application.Mapping;
using UserService.Domain.Entities;
using UserService.Domain.Exceptions;
using UserService.Domain.Interfaces;

namespace UserService.Application.Services;

/// <summary>
/// Classic application service that orchestrates the User use cases.
/// It coordinates the domain entity, the repository and the password
/// hasher — but holds no business invariants itself (those live in the entity).
/// </summary>
public sealed class UserAppService : IUserService
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher _passwordHasher;

    public UserAppService(IUserRepository repository, IPasswordHasher passwordHasher)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _repository.EmailExistsAsync(email, ct))
            throw new DuplicateEmailException(email);

        var passwordHash = _passwordHasher.Hash(request.Password);

        var user = User.Create(
            firstName: request.FirstName,
            lastName: request.LastName,
            email: email,
            passwordHash: passwordHash,
            nationalId: request.NationalId,
            phone: request.Phone);

        await _repository.AddAsync(user, ct);
        await _repository.SaveChangesAsync(ct);

        return user.ToResponse();
    }

    public async Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _repository.GetByIdAsync(id, ct);
        return user?.ToResponse();
    }

    public async Task<UserResponse> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _repository.GetByIdAsync(id, ct)
                   ?? throw new UserNotFoundException(id);

        user.UpdateProfile(request.FirstName, request.LastName, request.Phone, request.ProfileImage);

        _repository.Update(user);
        await _repository.SaveChangesAsync(ct);

        return user.ToResponse();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _repository.GetByIdAsync(id, ct)
                   ?? throw new UserNotFoundException(id);

        _repository.Remove(user);
        await _repository.SaveChangesAsync(ct);
    }
}
