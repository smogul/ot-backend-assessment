using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace OT.Assessment.Infrastructure.Messaging;

public sealed class RabbitMqConnector(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqConnector> logger)
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    private readonly RabbitMqOptions _settings = options.Value;

    /// Like phoning a friend who has not got home yet. It rings out, so you wait 2 seconds and try again until they pick up.
    public async Task<IConnection> ConnectAsync(string clientName, CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _settings.HostName,
            Port = _settings.Port,
            UserName = _settings.UserName,
            Password = _settings.Password,
            ClientProvidedName = clientName
        };

        while (true)
        {
            try
            {
                return await factory.CreateConnectionAsync(cancellationToken);
            }
            catch (BrokerUnreachableException)
            {
                logger.LogWarning(
                    "RabbitMQ is not reachable at {Host}:{Port}. Trying again soon.",
                    _settings.HostName,
                    _settings.Port);

                await Task.Delay(RetryDelay, cancellationToken);
            }
        }
    }

    /// Like a shop opening with two lines: the main checkout, and a help desk for customers with a problem. A customer the checkout cannot serve is sent to the help desk, not sent home.
    public async Task DeclareQueuesAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.QueueDeclareAsync(
            queue: _settings.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        var mainQueueSettings = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = string.Empty,
            ["x-dead-letter-routing-key"] = _settings.DeadLetterQueueName
        };

        await channel.QueueDeclareAsync(
            queue: _settings.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: mainQueueSettings,
            cancellationToken: cancellationToken);
    }
}
