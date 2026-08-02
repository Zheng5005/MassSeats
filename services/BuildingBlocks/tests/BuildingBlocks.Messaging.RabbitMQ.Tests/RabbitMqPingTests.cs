using BuildingBlocks.Messaging.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace BuildingBlocks.Messaging.RabbitMQ.Tests;

public sealed class RabbitMqPingTests
{
    [Fact]
    public async Task PublishAsync_WhenMessageIsUnroutable_Throws()
    {
        SkipUnlessEnabled();

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(CreateConfiguration(GetPort()));
        builder.Services.AddRabbitMqMessaging(builder.Configuration);

        using var host = builder.Build();
        var cancellationToken = TestContext.Current.CancellationToken;
        await host.StartAsync(cancellationToken);

        var publisher = host.Services.GetRequiredService<IEventPublisher>();
        await Assert.ThrowsAsync<PublishReturnException>(() =>
            publisher.PublishAsync(new UnroutablePingEvent(), cancellationToken));

        await host.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task PublishAsync_WithRegisteredConsumer_DeliversPing()
    {
        SkipUnlessEnabled();

        var port = GetPort();
        var queueName = $"massseats.ping.{Guid.NewGuid():N}.queue";
        var probe = new PingProbe();

        var builder = Host.CreateApplicationBuilder();
        var configuration = CreateConfiguration(port);
        configuration["RabbitMq:QueueName"] = queueName;
        builder.Configuration.AddInMemoryCollection(configuration);
        builder.Services.AddSingleton(probe);
        builder.Services.AddRabbitMqMessaging(builder.Configuration);
        builder.Services.AddEventConsumer<PingEvent, PingConsumer>();

        using var host = builder.Build();
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await host.StartAsync(cancellationToken);

            var ping = new PingEvent("pong");
            var publisher = host.Services.GetRequiredService<IEventPublisher>();
            await publisher.PublishAsync(ping, cancellationToken);

            var received = await probe.Received.Task.WaitAsync(
                TimeSpan.FromSeconds(10),
                cancellationToken);

            Assert.Equal(ping.Id, received.Id);
            Assert.Equal("pong", received.Value);
        }
        finally
        {
            await host.StopAsync(cancellationToken);
            await DeleteQueuesAsync(port, queueName, cancellationToken);
        }
    }

    [Fact]
    public async Task ConsumeAsync_WhenHandlerRecovers_RetriesUntilSuccessful()
    {
        SkipUnlessEnabled();

        var port = GetPort();
        var queueName = $"massseats.retry.{Guid.NewGuid():N}.queue";
        var probe = new RetryProbe(failuresBeforeSuccess: 2);
        var builder = CreateConsumerHostBuilder(port, queueName);
        builder.Services.AddSingleton(probe);
        builder.Services.AddEventConsumer<PingEvent, RecoveringPingConsumer>();

        using var host = builder.Build();
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await host.StartAsync(cancellationToken);
            var ping = new PingEvent("eventually-pong");

            await host.Services
                .GetRequiredService<IEventPublisher>()
                .PublishAsync(ping, cancellationToken);

            var received = await probe.Received.Task.WaitAsync(
                TimeSpan.FromSeconds(10),
                cancellationToken);

            Assert.Equal(ping.Id, received.Id);
            Assert.Equal(3, probe.Attempts);
        }
        finally
        {
            await host.StopAsync(cancellationToken);
            await DeleteQueuesAsync(port, queueName, cancellationToken);
        }
    }

    [Fact]
    public async Task ConsumeAsync_WhenRetryBudgetIsExhausted_MovesMessageToDeadLetterQueue()
    {
        SkipUnlessEnabled();

        var port = GetPort();
        var queueName = $"massseats.poison.{Guid.NewGuid():N}.queue";
        var deadLetterQueueName = queueName.Replace(".queue", ".dlq", StringComparison.Ordinal);
        var probe = new PoisonProbe();
        var builder = CreateConsumerHostBuilder(port, queueName);
        builder.Services.AddSingleton(probe);
        builder.Services.AddEventConsumer<PingEvent, PoisonPingConsumer>();

        using var host = builder.Build();
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await host.StartAsync(cancellationToken);
            var ping = new PingEvent("poison");

            await host.Services
                .GetRequiredService<IEventPublisher>()
                .PublishAsync(ping, cancellationToken);

            var deadLetter = await WaitForMessageAsync(
                port,
                deadLetterQueueName,
                cancellationToken);

            Assert.Equal(ping.Id.ToString(), deadLetter.BasicProperties.MessageId);
            Assert.Equal(3, probe.Attempts);
        }
        finally
        {
            await host.StopAsync(cancellationToken);
            await DeleteQueuesAsync(port, queueName, cancellationToken);
        }
    }

    private static void SkipUnlessEnabled() =>
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable("RABBITMQ_INTEGRATION_TESTS") == "true",
            "Set RABBITMQ_INTEGRATION_TESTS=true to run RabbitMQ integration tests.");

    private static int GetPort() =>
        int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out var port)
            ? port
            : 5672;

    private static Dictionary<string, string?> CreateConfiguration(int port) => new()
    {
        ["RabbitMq:Host"] = "localhost",
        ["RabbitMq:Port"] = port.ToString(),
        ["RabbitMq:UserName"] = "guest",
        ["RabbitMq:Password"] = "guest",
        ["RabbitMq:VirtualHost"] = "/"
    };

    private static HostApplicationBuilder CreateConsumerHostBuilder(int port, string queueName)
    {
        var builder = Host.CreateApplicationBuilder();
        var configuration = CreateConfiguration(port);
        configuration["RabbitMq:QueueName"] = queueName;
        configuration["RabbitMq:MaxRetryAttempts"] = "2";
        configuration["RabbitMq:RetryDelay"] = "00:00:00.100";
        builder.Configuration.AddInMemoryCollection(configuration);
        builder.Services.AddRabbitMqMessaging(builder.Configuration);
        return builder;
    }

    private static async Task DeleteQueuesAsync(
        int port,
        string queueName,
        CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            Port = port,
            UserName = "guest",
            Password = "guest"
        };

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await channel.QueueDeleteAsync(queueName, cancellationToken: cancellationToken);
        await channel.QueueDeleteAsync(
            queueName.Replace(".queue", ".retry", StringComparison.Ordinal),
            cancellationToken: cancellationToken);
        await channel.QueueDeleteAsync(
            queueName.Replace(".queue", ".dlq", StringComparison.Ordinal),
            cancellationToken: cancellationToken);
    }

    private static async Task<BasicGetResult> WaitForMessageAsync(
        int port,
        string queueName,
        CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            Port = port,
            UserName = "guest",
            Password = "guest"
        };

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);

        while (DateTimeOffset.UtcNow < deadline)
        {
            var message = await channel.BasicGetAsync(
                queueName,
                autoAck: true,
                cancellationToken);
            if (message is not null)
                return message;

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        throw new TimeoutException($"No message arrived in '{queueName}' within 10 seconds.");
    }

    private sealed record PingEvent(string Value) : IntegrationEvent;
    private sealed record UnroutablePingEvent : IntegrationEvent;

    private sealed class PingProbe
    {
        public TaskCompletionSource<PingEvent> Received { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class PingConsumer(PingProbe probe) : IEventConsumer<PingEvent>
    {
        public Task HandleAsync(PingEvent @event, CancellationToken cancellationToken = default)
        {
            probe.Received.TrySetResult(@event);
            return Task.CompletedTask;
        }
    }

    private sealed class RetryProbe(int failuresBeforeSuccess)
    {
        private int _attempts;

        public int Attempts => _attempts;
        public TaskCompletionSource<PingEvent> Received { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool ShouldFail() => Interlocked.Increment(ref _attempts) <= failuresBeforeSuccess;
    }

    private sealed class RecoveringPingConsumer(RetryProbe probe) : IEventConsumer<PingEvent>
    {
        public Task HandleAsync(PingEvent @event, CancellationToken cancellationToken = default)
        {
            if (probe.ShouldFail())
                throw new InvalidOperationException("Transient test failure.");

            probe.Received.TrySetResult(@event);
            return Task.CompletedTask;
        }
    }

    private sealed class PoisonProbe
    {
        private int _attempts;

        public int Attempts => _attempts;
        public void RecordAttempt() => Interlocked.Increment(ref _attempts);
    }

    private sealed class PoisonPingConsumer(PoisonProbe probe) : IEventConsumer<PingEvent>
    {
        public Task HandleAsync(PingEvent @event, CancellationToken cancellationToken = default)
        {
            probe.RecordAttempt();
            throw new InvalidOperationException("Poison test message.");
        }
    }
}
