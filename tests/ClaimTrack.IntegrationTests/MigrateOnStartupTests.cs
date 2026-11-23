using AwesomeAssertions;
using ClaimTrack.Api.Data;
using ClaimTrack.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ClaimTrack.IntegrationTests;

// Database:MigrateOnStartup (used by docker compose). Each test points a copy of the API at a
// brand-new, empty database in the test container.
[Collection(ApiCollection.Name)]
public class MigrateOnStartupTests(ClaimTrackApiFactory factory)
{
    private string NewDatabaseConnectionString() =>
        new SqlConnectionStringBuilder(factory.DatabaseConnectionString)
        {
            InitialCatalog = $"MigrateTest_{Guid.NewGuid():N}"
        }.ConnectionString;

    private void StartApi(string connectionString, bool migrateOnStartup)
    {
        using var api = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ClaimTrack"] = connectionString,
                    ["Database:MigrateOnStartup"] = migrateOnStartup.ToString()
                })));

        api.CreateClient(); // starts the API, which runs the startup code
    }

    private static AppDbContext ContextFor(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options);

    [Fact]
    public async Task Startup_WithMigrateOnStartup_CreatesTheDatabaseWithEveryMigration()
    {
        var connectionString = NewDatabaseConnectionString();

        StartApi(connectionString, migrateOnStartup: true);

        await using var db = ContextFor(connectionString);
        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        (await db.Database.GetAppliedMigrationsAsync()).Should().Equal(db.Database.GetMigrations());
        (await db.Claims.CountAsync()).Should().Be(0); // the tables exist and can be queried
    }

    [Fact]
    public async Task Startup_WithoutMigrateOnStartup_LeavesTheDatabaseAlone()
    {
        var connectionString = NewDatabaseConnectionString();

        StartApi(connectionString, migrateOnStartup: false);

        await using var db = ContextFor(connectionString);
        (await db.Database.CanConnectAsync()).Should().BeFalse("the database was never created");
    }
}
