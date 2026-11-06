using OT.Assessment.App.Swagger;
using OT.Assessment.Application;
using OT.Assessment.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SchemaFilter<CasinoWagerRequestExample>());
builder.Services.AddApplication();
builder.Services.AddApiInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();
