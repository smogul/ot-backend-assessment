using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OT.Assessment.Application.Abstractions;
using OT.Assessment.Domain.Entities;
using RabbitMQ.Client;

namespace OT.Assessment.Infrastructure.Messaging;

public sealed class RabbitMqWagerPublisher(
    RabbitMqConnector connector,
    IOptions<RabbitMqOptions> options) : IWagerPublisher, IHostedService
{
    private static readonly CreateChannelOptions PublisherChannelOptions = new(
        publisherConfirmationsEnabled: true,
        publisherConfirmationTrackingEnabled: true);

    private readonly RabbitMqOptions _settings = options.Value;
    private readonly Channel<IChannel> _channelBox = Channel.CreateUnbounded<IChannel>();
    private IConnection? _connection;

    /// Like a shop opening its tills before unlocking the door. When 500 customers rush in at once, every till is already open.
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _connection = await connector.ConnectAsync("ot-assessment-api", cancellationToken);

        await using var setupChannel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await connector.DeclareQueuesAsync(setupChannel, cancellationToken);

        for (var i = 0; i < _settings.PublisherChannelCount; i++)
        {
            var channel = await _connection.CreateChannelAsync(PublisherChannelOptions, cancellationToken);
            await _channelBox.Writer.WriteAsync(channel, cancellationToken);
        }
    }

    /// Thabo bets R50. We take a channel from the box, send his bet, wait for RabbitMQ's receipt, and put the channel back. If RabbitMQ cannot take the bet, an error is thrown and the API reports a failure, so the sender knows the bet was not accepted.
    public async Task PublishAsync(CasinoWager wager, CancellationToken cancellationToken)
    {
        var channel = await _channelBox.Reader.ReadAsync(cancellationToken);

        try
        {
            if (channel.IsClosed)
            {
                channel = await _connection!.CreateChannelAsync(PublisherChannelOptions, cancellationToken);
            }

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = wager.WagerId.ToString()
            };

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: _settings.QueueName,
                mandatory: true,
                basicProperties: properties,
                body: WagerMessageSerializer.ToBytes(wager),
                cancellationToken: cancellationToken);
        }
        finally
        {
            _channelBox.Writer.TryWrite(channel);
        }
    }

    /// Like hanging up the phone politely at the end of a call.
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            await _connection.CloseAsync(cancellationToken);
        }
    }
}
