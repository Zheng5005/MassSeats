using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BuildingBlocks.Messaging.RabbitMQ;

internal sealed class RabbitMqConsumerHostedService : BackgroundService
{
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
                publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false,
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
                _logger.LogError(
                    exception,
                    "Failed to consume RabbitMQ message {MessageId} of type {MessageType}",
                    delivery.BasicProperties.MessageId,
                    delivery.BasicProperties.Type);

                await channel.BasicNackAsync(
                    delivery.DeliveryTag,
                    multiple: false,
                    requeue: false,
                    stoppingToken);
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
}
