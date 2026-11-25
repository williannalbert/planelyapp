using PlanelyApp.Api;
using PlanelyApp.Api.Authentication;
using PlanelyApp.Api.Configuration;
using PlanelyApp.Api.Documentation;
using PlanelyApp.Api.Endpoints.Account;
using PlanelyApp.Api.Logging;
using PlanelyApp.Api.Tenancy;
using PlanelyApp.Application;
using PlanelyApp.Infrastructure;
using PlanelyApp.Api.Endpoints.Organization;
using PlanelyApp.Api.Roles;
using PlanelyApp.Infrastructure.Development;
using PlanelyApp.Infrastructure.Identity;
using PlanelyApp.Infrastructure.Imports;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.Services.AddApiLogging(builder.Environment);

builder.Services.AddApplication();

builder.Services.AddInfrastructure(configuration.GetRequiredConnectionString("Default"));
builder.Services.AddRabbitMqPublisher(configuration.GetRequiredConnectionString("RabbitMq"));
builder.Services.AddOutboxProcessing();
builder.Services.AddReminderScheduling();
builder.Services.AddImportProcessing();

var keycloak = configuration.GetRequiredOptions<KeycloakOptions>("Keycloak");
builder.Services.AddKeycloakIdentityProvider(keycloak);
builder.Services.AddCustomerTokenIssuer(configuration.GetRequiredOptions<CustomerTokenOptions>("CustomerToken"));


builder.Services.AddApiBasics();
builder.Services.AddApiAuthentication(keycloak, builder.Environment);
builder.Services.AddApiDocumentation();
builder.Services.AddPendingPieces();

if (builder.Environment.IsDevelopment())
{
    // The user of the realm of development needs to exist in the database too (see api-authentication.md).
    builder.Services.AddDevelopmentSeed(configuration.GetRequiredOptions<DevelopmentSeedOptions>("DevelopmentSeed"));
}

var app = builder.Build();

app.UseApiBasics();
app.UseApiAuthentication();
app.UseApiTenancy();
app.UseApiRoles();
app.MapApiBasics();
app.MapApiDocumentation(app.Environment);
app.MapMe();
app.MapCompanies();

app.Run();

// The tests of the whole Api (5.10) start it through this type.
public partial class Program;