using PaymentService.Application.Interfaces;
using PaymentService.Domain.Interfaces;
using PaymentService.Infrastructure.Configuration;
using PaymentService.Infrastructure.Gateways;
using PaymentService.Infrastructure.Messaging;
using PaymentService.Infrastructure.Persistence;
using BuildingBlocks.Messaging.Contracts;
using BuildingBlocks.Messaging.RabbitMQ;
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

        services.AddOptions<PaymentOptions>()
            .Bind(configuration.GetSection("Payment"))
            .Validate(
                options => options.Currency is { Length: 3 } &&
                    !string.IsNullOrWhiteSpace(options.Currency),
                "Payment:Currency must be a three-letter ISO currency code.")
            .ValidateOnStart();

        var stripeOptions = new StripeOptions
        {
            SecretKey = configuration["Stripe:SecretKey"]
                ?? throw new InvalidOperationException("'Stripe:SecretKey' is not configured."),
            WebhookSecret = configuration["Stripe:WebhookSecret"]
                ?? throw new InvalidOperationException("'Stripe:WebhookSecret' is not configured."),
        };
        services.AddSingleton(stripeOptions);
        services.AddScoped<IPaymentGateway, StripePaymentGateway>();

        services.AddRabbitMqMessaging(configuration);
        services.AddEventConsumer<SeatReserved, SeatReservedConsumer>();

        return services;
    }
}
