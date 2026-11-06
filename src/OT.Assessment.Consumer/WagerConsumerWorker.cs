using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using OT.Assessment.Application.Abstractions;
using OT.Assessment.Domain.Entities;
using OT.Assessment.Infrastructure.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace OT.Assessment.Consumer;

public sealed class WagerConsumerWorker(
    RabbitMqConnector connector,
    IWagerWriter wagerWriter,
    IOptions<RabbitMqOptions> options,
    ILogger<WagerConsumerWorker> logger) : BackgroundService
{
    private readonly IWagerWriter _wagerWriter = wagerWriter;
    private readonly RabbitMqOptions _settings = options.Value;
    private readonly Channel<ReceivedMessage> _inbox = Channel.CreateUnbounded<ReceivedMessage>();

    internal TimeSpan SaveRetryDelay { get; init; } = TimeSpan.FromSeconds(5);

    internal ChannelWriter<ReceivedMessage> InboxWriter => _inbox.Writer;

    /// With a prefetch of 1000, the conveyor belt pauses when we are holding 1000 parcels we have not signed for. It carries on as we sign for them.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var connection = await connector.ConnectAsync("ot-assessment-consumer", stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await connector.DeclareQueuesAsync(channel, stoppingToken);
        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: _settings.PrefetchCount,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += PutMessageInInboxAsync;

        await channel.BasicConsumeAsync(
            queue: _settings.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation("Consumer is listening to queue {Queue}.", _settings.QueueName);

        while (!stoppingToken.IsCancellationRequested)
        {
            var batch = await ReadNextBatchAsync(stoppingToken);

            try
            {
                await SaveAndAcknowledgeAsync(channel, batch, stoppingToken);
            }
            catch (AlreadyClosedException)
            {
                logger.LogWarning(
                    "Lost the RabbitMQ connection before signing for {Count} messages. They will be delivered again.",
                    batch.Count);
            }
        }
    }

    /// Like taking a parcel off the conveyor belt and placing it in the inbox tray. No storing happens here, so the belt can keep moving.
    private async Task PutMessageInInboxAsync(object sender, BasicDeliverEventArgs delivery)
    {
        var message = new ReceivedMessage(delivery.DeliveryTag, delivery.Body.ToArray());

        await _inbox.Writer.WriteAsync(message, delivery.CancellationToken);
    }

    /// You wait for the first parcel. Then you scoop up everything in the tray, up to 500. If there are 3 parcels, your pile is 3. If there are 900, your pile is 500 and the rest wait for the next trip.
    internal async Task<List<ReceivedMessage>> ReadNextBatchAsync(CancellationToken cancellationToken)
    {
        var firstMessage = await _inbox.Reader.ReadAsync(cancellationToken);
        var batch = new List<ReceivedMessage> { firstMessage };

        while (batch.Count < _settings.BatchSize && _inbox.Reader.TryRead(out var nextMessage))
        {
            batch.Add(nextMessage);
        }

        return batch;
    }

    /// You put parcels 1 to 500 on the shelf, then sign once: "everything up to number 500 is stored". If the shelf breaks before you sign, the parcels get delivered again. That is safe, because the database skips any wager it already has.
    internal async Task SaveAndAcknowledgeAsync(
        IChannel channel,
        List<ReceivedMessage> batch,
        CancellationToken cancellationToken)
    {
        var wagers = new List<CasinoWager>();
        ulong lastGoodDeliveryTag = 0;

        foreach (var message in batch)
        {
            try
            {
                wagers.Add(WagerMessageSerializer.FromBytes(message.Body));
                lastGoodDeliveryTag = message.DeliveryTag;
            }
            catch (JsonException exception)
            {
                logger.LogError(
                    exception,
                    "Message {DeliveryTag} is not a valid wager. Sending it to the dead letter queue.",
                    message.DeliveryTag);

                await channel.BasicNackAsync(message.DeliveryTag, multiple: false, requeue: false, cancellationToken);
            }
        }

        if (wagers.Count == 0)
        {
            return;
        }

        await SaveUntilItWorksAsync(wagers, cancellationToken);
        await channel.BasicAckAsync(lastGoodDeliveryTag, multiple: true, cancellationToken);

        logger.LogInformation("Saved {Count} wagers.", wagers.Count);
    }

    /// The storeroom door is locked, so you stand there holding the parcels and try the door every 5 seconds. You do not throw the parcels away.
    private async Task SaveUntilItWorksAsync(List<CasinoWager> wagers, CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                await _wagerWriter.SaveBatchAsync(wagers, cancellationToken);
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(
                    exception,
                    "Could not save {Count} wagers. Trying again in {Delay}.",
                    wagers.Count,
                    SaveRetryDelay);

                await Task.Delay(SaveRetryDelay, cancellationToken);
            }
        }
    }
}