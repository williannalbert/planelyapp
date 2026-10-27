using Microsoft.EntityFrameworkCore;

namespace PlanelyApp.Infrastructure.Persistence;

internal static class DatabaseServer
{
    // Fixed on purpose: ServerVersion.AutoDetect would open a connection while the services are registered.
    // Keep it equal to the image in infra/docker-compose.yml.
    public static readonly ServerVersion Version = new MySqlServerVersion(new Version(8, 4, 0));
}