using UserService.Application.DTOs;
using UserService.Application.Interfaces;

namespace UserService.API.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users").WithTags("Users");

        group.MapPost("/", async (CreateUserRequest request, IUserService service, CancellationToken ct) =>
        {
            var user = await service.CreateAsync(request, ct);
            return Results.Created($"/users/{user.Id}", user);
        });

        group.MapGet("/{id:guid}", async (Guid id, IUserService service, CancellationToken ct) =>
        {
            var user = await service.GetByIdAsync(id, ct);
            return user is null ? Results.NotFound() : Results.Ok(user);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request, IUserService service, CancellationToken ct) =>
        {
            var user = await service.UpdateAsync(id, request, ct);
            return Results.Ok(user);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IUserService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        });

        group.MapPost("/login", async (LoginRequest request, IAuthService authService, CancellationToken ct) =>
        {
            var response = await authService.LoginAsync(request, ct);
            return Results.Ok(response);
        });

        return app;
    }
}
