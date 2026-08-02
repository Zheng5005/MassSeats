using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BuildingBlocks.Messaging.RabbitMQ;

internal sealed class RabbitMqConsumerHostedService : BackgroundService
{
    private const string RetryCountHeader = "x-retry-count";
    private static readonly TimeSpan HandoffFailureBackoff = TimeSpan.FromSeconds(1);

    private readonly RabbitMqConnection _connection;
    private readonly TopologyInitializer _topology;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqConsumerHostedService> _logger;
    private readonly IReadOnlyDictionary<string, IEventConsumerRegistration> _registrations;

    public RabbitMqConsumerHostedService(
        RabbitMqConnection connection,
        TopologyInitializer topology,
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        IEnumerable<IEventConsumerRegistration> registrations,
        ILogger<RabbitMqConsumerHostedService> logger)
    {
        _connection = connection;
        _topology = topology;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _registrations = registrations.ToDictionary(registration => registration.EventName);
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_registrations.Count > 0 && string.IsNullOrWhiteSpace(_options.QueueName))
            throw new InvalidOperationException(
                "RabbitMq:QueueName must be configured when event consumers are registered.");

        await _topology.InitializeAsync(
            _registrations.Values.Select(registration => registration.EventType),
            cancellationToken);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.QueueName))
            return;

        await using var channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true,
                consumerDispatchConcurrency: 1),
            stoppingToken);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: _options.PrefetchCount,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try
            {
                var eventName = delivery.BasicProperties.Type;
                if (string.IsNullOrWhiteSpace(eventName) ||
                    !_registrations.TryGetValue(eventName, out var registration))
                {
                    throw new InvalidOperationException(
                        $"No event consumer is registered for message type '{eventName ?? "<missing>"}'.");
                }

                await using var scope = _scopeFactory.CreateAsyncScope();
                await registration.HandleAsync(
                    scope.ServiceProvider,
                    delivery.Body,
                    stoppingToken);

                await channel.BasicAckAsync(
                    delivery.DeliveryTag,
                    multiple: false,
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // The channel is closing; RabbitMQ will redeliver the unacked message.
            }
            catch (Exception exception)
            {
                var retryCount = GetRetryCount(delivery.BasicProperties.Headers);
                _logger.LogError(
                    exception,
                    "Failed to consume RabbitMQ message {MessageId} of type {MessageType} on attempt {Attempt}",
                    delivery.BasicProperties.MessageId,
                    delivery.BasicProperties.Type,
                    retryCount < int.MaxValue ? retryCount + 1 : int.MaxValue);

                try
                {
                    await RepublishFailedMessageAsync(
                        channel,
                        delivery,
                        retryCount,
                        stoppingToken);

                    await channel.BasicAckAsync(
                        delivery.DeliveryTag,
                        multiple: false,
                        stoppingToken);
                }
                catch (Exception publishException)
                {
                    _logger.LogError(
                        publishException,
                        "Could not hand off failed RabbitMQ message {MessageId}; requeueing the original",
                        delivery.BasicProperties.MessageId);

                    try
                    {
                        await Task.Delay(HandoffFailureBackoff, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        // Leave the delivery unacked; channel shutdown will requeue it.
                        return;
                    }

                    await channel.BasicNackAsync(
                        delivery.DeliveryTag,
                        multiple: false,
                        requeue: true,
                        stoppingToken);
                }
            }
        };

        await channel.BasicConsumeAsync(
            _options.QueueName,
            autoAck: false,
            consumerTag: string.Empty,
            noLocal: false,
            exclusive: false,
            arguments: null,
            consumer,
            stoppingToken);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private async Task RepublishFailedMessageAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        int retryCount,
        CancellationToken cancellationToken)
    {
        var shouldRetry = retryCount < _options.MaxRetryAttempts;
        var properties = CopyProperties(delivery.BasicProperties);
        properties.Expiration = null;

        if (shouldRetry)
        {
            properties.Headers![RetryCountHeader] = retryCount + 1;
            properties.Expiration = checked((int)_options.RetryDelay.TotalMilliseconds).ToString();
        }

        var exchange = shouldRetry
            ? _options.RetryExchangeName
            : _options.DeadLetterExchangeName;

        await channel.BasicPublishAsync(
            exchange,
            _options.QueueName!,
            mandatory: true,
            properties,
            delivery.Body,
            cancellationToken);

        if (shouldRetry)
        {
            _logger.LogWarning(
                "Scheduled retry {RetryAttempt}/{MaxRetryAttempts} for RabbitMQ message {MessageId} after {RetryDelay}",
                retryCount + 1,
                _options.MaxRetryAttempts,
                delivery.BasicProperties.MessageId,
                _options.RetryDelay);
        }
        else
        {
            _logger.LogError(
                "Moved RabbitMQ message {MessageId} to the dead-letter queue after {RetryAttempts} retries",
                delivery.BasicProperties.MessageId,
                retryCount);
        }
    }

    private static int GetRetryCount(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue(RetryCountHeader, out var value))
            return 0;

        return value switch
        {
            byte count => count,
            sbyte count when count >= 0 => count,
            short count when count >= 0 => count,
            int count when count >= 0 => count,
            long count when count is >= 0 and <= int.MaxValue => (int)count,
            _ => int.MaxValue
        };
    }

    private static BasicProperties CopyProperties(IReadOnlyBasicProperties source) => new()
    {
        AppId = source.AppId,
        ClusterId = source.ClusterId,
        ContentEncoding = source.ContentEncoding,
        ContentType = source.ContentType,
        CorrelationId = source.CorrelationId,
        DeliveryMode = source.DeliveryMode,
        Expiration = source.Expiration,
        Headers = source.Headers is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(source.Headers),
        MessageId = source.MessageId,
        Priority = source.Priority,
        ReplyTo = source.ReplyTo,
        Timestamp = source.Timestamp,
        Type = source.Type,
        UserId = source.UserId
    };
}
