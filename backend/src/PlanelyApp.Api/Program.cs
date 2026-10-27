using PlanelyApp.Infrastructure;
using PlanelyApp.Application;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("The connection string 'Default' is missing.");

var rabbitMqConnectionString = builder.Configuration.GetConnectionString("RabbitMq")
    ?? throw new InvalidOperationException("The connection string 'RabbitMq' is missing.");

builder.Services.AddApplication();

builder.Services.AddInfrastructure(connectionString);
builder.Services.AddRabbitMqPublisher(rabbitMqConnectionString);
builder.Services.AddOutboxProcessing();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();