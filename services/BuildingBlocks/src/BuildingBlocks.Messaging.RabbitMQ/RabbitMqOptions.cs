namespace BuildingBlocks.Messaging.RabbitMQ;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string UserName { get; init; } = "guest";
    public string Password { get; init; } = "guest";
    public string VirtualHost { get; init; } = "/";
    public string ExchangeName { get; init; } = "massseats.events";
    public string DeadLetterExchangeName { get; init; } = "massseats.events.dead-letter";
    public string? QueueName { get; init; }
    public ushort PrefetchCount { get; init; } = 16;
    public TimeSpan NetworkRecoveryInterval { get; init; } = TimeSpan.FromSeconds(5);
}
