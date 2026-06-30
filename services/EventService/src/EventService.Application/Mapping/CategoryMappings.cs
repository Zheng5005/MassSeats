using EventService.Application.DTOs;
using EventService.Domain.Entities;

namespace EventService.Application.Mapping;

/// <summary>
/// Manual mapping entity -> DTO. Kept explicit on purpose so the
/// projection is obvious and testable.
/// </summary>
internal static class CategoryMappings
{
    public static CategoryResponse ToResponse(this Category category) => new(
        category.Id,
        category.Name,
        category.Description,
        category.CreatedAt,
        category.UpdatedAt);
}
