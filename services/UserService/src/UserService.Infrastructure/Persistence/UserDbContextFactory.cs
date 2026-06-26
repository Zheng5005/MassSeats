using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UserService.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used only by the EF Core CLI (migrations).
/// Keeps the API project free of the EF Design dependency.
/// The connection string here is for tooling, not runtime.
/// </summary>
public sealed class UserDbContextFactory : IDesignTimeDbContextFactory<UserDbContext>
{
    public UserDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<UserDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=userservice;Username=postgres;Password=postgres")
            .Options;

        return new UserDbContext(options);
    }
}
