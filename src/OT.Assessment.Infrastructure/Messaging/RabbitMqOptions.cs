namespace OT.Assessment.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string QueueName { get; set; } = "casino-wagers";

    public string DeadLetterQueueName { get; set; } = "casino-wagers-dead-letter";

    public int PublisherChannelCount { get; set; } = 20;

    public ushort PrefetchCount { get; set; } = 1000;

    public int BatchSize { get; set; } = 500;
}
