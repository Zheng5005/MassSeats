using UserService.Application.DTOs;
using UserService.Domain.Entities;

namespace UserService.Application.Mapping;

/// <summary>
/// Manual mapping entity -> DTO. Kept explicit on purpose so the
/// projection (and what we choose NOT to expose, e.g. PasswordHash)
/// is obvious and testable.
/// </summary>
internal static class UserMappings
{
    public static UserResponse ToResponse(this User user) => new(
        user.Id,
        user.FirstName,
        user.LastName,
        user.Email,
        user.NationalId,
        user.ProfileImage,
        user.Phone,
        user.CreatedAt,
        user.UpdatedAt);
}
