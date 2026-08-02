using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace BuildingBlocks.Messaging.RabbitMQ;

public sealed class TopologyInitializer
{
    private readonly RabbitMqConnection _connection;
    private readonly RabbitMqOptions _options;

    public TopologyInitializer(
        RabbitMqConnection connection,
        IOptions<RabbitMqOptions> options)
    {
        _connection = connection;
        _options = options.Value;
    }

    public async Task InitializeAsync(
        IEnumerable<Type> subscribedEventTypes,
        CancellationToken cancellationToken = default)
    {
        await using var channel = await _connection.CreateChannelAsync(
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            _options.ExchangeName,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(_options.QueueName))
            return;

        await channel.ExchangeDeclareAsync(
            _options.RetryExchangeName,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            _options.DeadLetterExchangeName,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var deadLetterQueueName = GetDeadLetterQueueName(_options.QueueName);
        var retryQueueName = GetRetryQueueName(_options.QueueName);

        await channel.QueueDeclareAsync(
            deadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            deadLetterQueueName,
            _options.DeadLetterExchangeName,
            _options.QueueName,
            cancellationToken: cancellationToken);

        var retryQueueArguments = new Dictionary<string, object?>
        {
            ["x-queue-type"] = "quorum",
            ["x-dead-letter-strategy"] = "at-least-once",
            ["x-overflow"] = "reject-publish",
            ["x-dead-letter-exchange"] = _options.ExchangeName,
            ["x-dead-letter-routing-key"] = _options.QueueName
        };

        await channel.QueueDeclareAsync(
            retryQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: retryQueueArguments,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            retryQueueName,
            _options.RetryExchangeName,
            _options.QueueName,
            cancellationToken: cancellationToken);

        var queueArguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = _options.DeadLetterExchangeName,
            ["x-dead-letter-routing-key"] = _options.QueueName
        };

        await channel.QueueDeclareAsync(
            _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArguments,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            _options.QueueName,
            _options.ExchangeName,
            _options.QueueName,
            cancellationToken: cancellationToken);

        foreach (var eventType in subscribedEventTypes.Distinct())
        {
            await channel.QueueBindAsync(
                _options.QueueName,
                _options.ExchangeName,
                RoutingKeyResolver.For(eventType),
                cancellationToken: cancellationToken);
        }
    }

    public async Task BindAsync<TEvent>(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.QueueName))
            throw new InvalidOperationException(
                "RabbitMq:QueueName must be configured before subscribing to events.");

        await InitializeAsync([typeof(TEvent)], cancellationToken);
    }

    internal static string GetDeadLetterQueueName(string queueName) =>
        queueName.EndsWith(".queue", StringComparison.Ordinal)
            ? $"{queueName[..^".queue".Length]}.dlq"
            : $"{queueName}.dlq";

    internal static string GetRetryQueueName(string queueName) =>
        queueName.EndsWith(".queue", StringComparison.Ordinal)
            ? $"{queueName[..^".queue".Length]}.retry"
            : $"{queueName}.retry";
}
