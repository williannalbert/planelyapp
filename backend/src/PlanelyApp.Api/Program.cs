using PlanelyApp.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("The connection string 'Default' is missing.");

builder.Services.AddInfrastructure(connectionString);

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();