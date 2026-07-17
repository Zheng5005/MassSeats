using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PaymentService.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used only by the EF Core CLI (migrations).
/// Keeps the API project free of the EF Design dependency.
/// The connection string here is for tooling, not runtime.
/// </summary>
public sealed class PaymentDbContextFactory : IDesignTimeDbContextFactory<PaymentDbContext>
{
    public PaymentDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=paymentservice;Username=postgres;Password=postgres")
            .Options;

        return new PaymentDbContext(options);
    }
}
