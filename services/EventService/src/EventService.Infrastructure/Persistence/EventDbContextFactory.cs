using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EventService.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used only by the EF Core CLI (migrations).
/// Keeps the API project free of the EF Design dependency.
/// The connection string here is for tooling, not runtime.
/// </summary>
public sealed class EventDbContextFactory : IDesignTimeDbContextFactory<EventDbContext>
{
    public EventDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<EventDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=eventservice;Username=postgres;Password=postgres")
            .Options;

        return new EventDbContext(options);
    }
}
