using BuildingBlocks.Messaging.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Messaging.RabbitMQ;

public static class DependencyInjection
{
    public static IServiceCollection AddRabbitMqMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Host),
                "RabbitMq:Host is required.")
            .Validate(
                options => options.Port is > 0 and <= 65535,
                "RabbitMq:Port must be a valid TCP port.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.UserName),
                "RabbitMq:UserName is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.VirtualHost),
                "RabbitMq:VirtualHost is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ExchangeName),
                "RabbitMq:ExchangeName is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.RetryExchangeName),
                "RabbitMq:RetryExchangeName is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.DeadLetterExchangeName),
                "RabbitMq:DeadLetterExchangeName is required.")
            .Validate(
                options => options.PrefetchCount > 0,
                "RabbitMq:PrefetchCount must be greater than zero.")
            .Validate(
                options => options.MaxRetryAttempts >= 0,
                "RabbitMq:MaxRetryAttempts cannot be negative.")
            .Validate(
                options => options.RetryDelay > TimeSpan.Zero &&
                    options.RetryDelay.TotalMilliseconds <= int.MaxValue,
                "RabbitMq:RetryDelay must be positive and no longer than 24.8 days.")
            .ValidateOnStart();

        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<TopologyInitializer>();
        services.AddSingleton<RabbitMqEventBus>();
        services.AddSingleton<IEventBus>(provider => provider.GetRequiredService<RabbitMqEventBus>());
        services.AddSingleton<IEventPublisher>(provider => provider.GetRequiredService<RabbitMqEventBus>());
        services.AddHostedService<RabbitMqConsumerHostedService>();

        return services;
    }

    public static IServiceCollection AddEventConsumer<TEvent, THandler>(
        this IServiceCollection services)
        where TEvent : IntegrationEvent
        where THandler : class, IEventConsumer<TEvent>
    {
        services.AddScoped<IEventConsumer<TEvent>, THandler>();
        services.AddSingleton<IEventConsumerRegistration, EventConsumerRegistration<TEvent>>();
        return services;
    }
}
