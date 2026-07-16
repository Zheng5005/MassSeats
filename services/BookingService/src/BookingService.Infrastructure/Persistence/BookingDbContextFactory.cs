using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BookingService.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used only by the EF Core CLI (migrations).
/// Keeps the API project free of the EF Design dependency.
/// The connection string here is for tooling, not runtime.
/// </summary>
public sealed class BookingDbContextFactory : IDesignTimeDbContextFactory<BookingDbContext>
{
    public BookingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=bookingservice;Username=postgres;Password=postgres")
            .Options;

        return new BookingDbContext(options);
    }
}
