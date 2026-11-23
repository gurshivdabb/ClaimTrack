using System.Net;
using AwesomeAssertions;
using ClaimTrack.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Serilog.Events;

namespace ClaimTrack.IntegrationTests;

// GET /health (ready: includes the database) and GET /health/live (process only), requirements §6.
[Collection(ApiCollection.Name)]
public class HealthCheckTests(ClaimTrackApiFactory factory)
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    public async Task Health_DatabaseUp_Returns200HealthyWithoutAToken(string url)
    {
        var response = await factory.CreateClient().GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task Health_DatabaseDown_ReadyIs503ButLiveIsStill200()
    {
        // The test SQL Server, but a database that doesn't exist, so connecting fails quickly.
        var missingDatabase = new SqlConnectionStringBuilder(factory.DatabaseConnectionString)
        {
            InitialCatalog = $"DoesNotExist_{Guid.NewGuid():N}"
        }.ConnectionString;
        using var api = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ClaimTrack"] = missingDatabase
                })));
        var client = api.CreateClient();

        var ready = await client.GetAsync("/health");
        var live = await client.GetAsync("/health/live");

        ready.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await ready.Content.ReadAsStringAsync();
        body.Should().Be("Unhealthy"); // no database name, server, or error message for callers
        live.StatusCode.Should().Be(HttpStatusCode.OK); // restarting the API wouldn't fix the database
    }

    [Fact]
    public async Task Health_SuccessfulProbe_IsNotLoggedAtInformation()
    {
        var client = factory.CreateClient();
        var marker = Guid.NewGuid().ToString("N");

        await client.GetAsync("/health");
        await client.GetAsync($"/api/nothing-here-{marker}"); // a normal request, logged afterwards

        // Once the normal request's line exists, the probe (sent first) has finished too.
        for (var attempt = 0; attempt < 50 && !factory.LogSink.Events.Any(e => Path(e)?.Contains(marker) == true); attempt++)
        {
            await Task.Delay(100);
        }
        factory.LogSink.Events.Should().NotContain(e =>
            Path(e) == "/health" && e.Level >= LogEventLevel.Information && Status(e) == "200");
    }

    private static string? Path(LogEvent e) =>
        e.Properties.TryGetValue("RequestPath", out var v) && v is ScalarValue { Value: string s } ? s : null;

    private static string? Status(LogEvent e) =>
        e.Properties.TryGetValue("StatusCode", out var v) && v is ScalarValue { Value: { } s } ? s.ToString() : null;
}
