using OT.Assessment.Consumer;
using OT.Assessment.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddConsumerInfrastructure(builder.Configuration);

builder.Services.AddHostedService<WagerConsumerWorker>();

builder.Build().Run();
