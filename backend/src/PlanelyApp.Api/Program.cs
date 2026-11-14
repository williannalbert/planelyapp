using PlanelyApp.Api;
using PlanelyApp.Api.Configuration;
using PlanelyApp.Application;
using PlanelyApp.Infrastructure;
using PlanelyApp.Infrastructure.Identity;
using PlanelyApp.Infrastructure.Imports;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.Services.AddApplication();

builder.Services.AddInfrastructure(configuration.GetRequiredConnectionString("Default"));
builder.Services.AddRabbitMqPublisher(configuration.GetRequiredConnectionString("RabbitMq"));
builder.Services.AddOutboxProcessing();
builder.Services.AddReminderScheduling();
builder.Services.AddImportProcessing();

builder.Services.AddKeycloakIdentityProvider(configuration.GetRequiredOptions<KeycloakOptions>("Keycloak"));
builder.Services.AddCustomerTokenIssuer(configuration.GetRequiredOptions<CustomerTokenOptions>("CustomerToken"));

builder.Services.AddApiBasics();
builder.Services.AddPendingPieces();

var app = builder.Build();

app.UseApiBasics();
app.MapApiBasics();

app.Run();

// The tests of the whole Api (5.10) start it through this type.
public partial class Program;