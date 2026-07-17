using PaymentService.Domain.Interfaces;
using PaymentService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PaymentService.Infrastructure;

/// <summary>
/// Wires up Infrastructure concretions (EF Core DbContext, repository,
/// Stripe gateway) behind the abstractions defined in the inner layers.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PaymentDb")
            ?? throw new InvalidOperationException("Connection string 'PaymentDb' is not configured.");

        services.AddDbContext<PaymentDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IPaymentRepository, PaymentRepository>();

        // TODO: register IPaymentGateway (Stripe SDK) when implemented

        return services;
    }
}
