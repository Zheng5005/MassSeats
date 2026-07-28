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
        var deadLetterQueueName = queueName.Replace(".queue", ".dlq", StringComparison.Ordinal);
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
            await DeleteQueuesAsync(
                port,
                queueName,
                deadLetterQueueName,
                cancellationToken);
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

    private static async Task DeleteQueuesAsync(
        int port,
        string queueName,
        string deadLetterQueueName,
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
        await channel.QueueDeleteAsync(deadLetterQueueName, cancellationToken: cancellationToken);
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
}
