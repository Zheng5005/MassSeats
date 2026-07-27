using System.Text.Json;
using BuildingBlocks.Messaging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace BuildingBlocks.Messaging.RabbitMQ;

public sealed class RabbitMqEventBus : IEventBus, IAsyncDisposable
{
    private readonly RabbitMqConnection _connection;
    private readonly TopologyInitializer _topology;
    private readonly RabbitMqOptions _options;
    private readonly SemaphoreSlim _publishLock = new(1, 1);
    private IChannel? _publishChannel;
    private int _disposed;

    public RabbitMqEventBus(
        RabbitMqConnection connection,
        TopologyInitializer topology,
        IOptions<RabbitMqOptions> options)
    {
        _connection = connection;
        _topology = topology;
        _options = options.Value;
    }

    public async Task PublishAsync<TEvent>(
        TEvent @event,
        CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(
            @event,
            RabbitMqJsonSerializerOptions.Instance);

        var properties = new BasicProperties
        {
            MessageId = @event.Id.ToString(),
            Type = typeof(TEvent).Name,
            ContentType = "application/json",
            Persistent = true,
            Timestamp = new AmqpTimestamp(@event.OccurredOn.ToUnixTimeSeconds())
        };

        await _publishLock.WaitAsync(cancellationToken);
        try
        {
            var channel = await GetPublishChannelAsync(cancellationToken);
            await channel.BasicPublishAsync(
                _options.ExchangeName,
                RoutingKeyResolver.For<TEvent>(),
                mandatory: true,
                properties,
                body,
                cancellationToken);
        }
        finally
        {
            _publishLock.Release();
        }
    }

    public Task SubscribeAsync<TEvent>(CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent =>
        _topology.BindAsync<TEvent>(cancellationToken);

    private async Task<IChannel> GetPublishChannelAsync(CancellationToken cancellationToken)
    {
        if (_publishChannel is not null)
        {
            if (_publishChannel.IsOpen || !_connection.IsOpen)
                return _publishChannel;

            await _publishChannel.DisposeAsync();
        }

        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);

        _publishChannel = await _connection.CreateChannelAsync(
            channelOptions,
            cancellationToken);

        return _publishChannel;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        await _publishLock.WaitAsync();
        try
        {
            if (_publishChannel is not null)
                await _publishChannel.DisposeAsync();

            _publishChannel = null;
        }
        finally
        {
            _publishLock.Release();
        }
    }
}
