using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OT.Assessment.Application.Abstractions;
using OT.Assessment.Infrastructure.Messaging;
using OT.Assessment.Infrastructure.Persistence;

namespace OT.Assessment.Infrastructure;

public static class DependencyInjection
{
    private const string ConnectionStringName = "OTAssessmentDb";

    /// When WagerService asks for an IWagerPublisher, the app now knows to hand it a RabbitMqWagerPublisher.
    public static IServiceCollection AddApiInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddSharedServices(services, configuration);

        services.AddSingleton<RabbitMqWagerPublisher>();
        services.AddSingleton<IWagerPublisher>(provider => provider.GetRequiredService<RabbitMqWagerPublisher>());
        services.AddHostedService(provider => provider.GetRequiredService<RabbitMqWagerPublisher>());
        services.AddSingleton<IWagerReader, SqlWagerReader>();

        return services;
    }

    /// When the consumer asks for an IWagerWriter, the app now knows to hand it a SqlWagerWriter.
    public static IServiceCollection AddConsumerInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddSharedServices(services, configuration);

        services.AddSingleton<IWagerWriter, SqlWagerWriter>();

        return services;
    }

    /// Like checking you have your house key before leaving home, rather than finding out at the front door later.
    private static void AddSharedServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddSingleton<RabbitMqConnector>();

        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Add the connection string '{ConnectionStringName}' to appsettings.json.");

        services.AddSingleton(new SqlConnectionFactory(connectionString));
    }
}
