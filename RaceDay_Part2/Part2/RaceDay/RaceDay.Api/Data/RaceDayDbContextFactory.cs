using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RaceDay.Api.Data;

/// <summary>
/// Used only by the EF Core tools (Add-Migration / Update-Database) so they never
/// have to start the whole web app. It reads the same connection string as the API.
/// </summary>
public class RaceDayDbContextFactory : IDesignTimeDbContextFactory<RaceDayDbContext>
{
    private const string FallbackConnection =
        "Server=(localdb)\\MSSQLLocalDB;Database=RaceDayApiDb;Trusted_Connection=True;TrustServerCertificate=True;";

    public RaceDayDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var options = new DbContextOptionsBuilder<RaceDayDbContext>()
            .UseSqlServer(config.GetConnectionString("DefaultConnection") ?? FallbackConnection)
            .Options;

        return new RaceDayDbContext(options);
    }
}
