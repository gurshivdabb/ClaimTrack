using System.Net.Http.Headers;
using ClaimTrack.Api.Data;
using ClaimTrack.Api.Services;
using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Serilog.Core;
using Testcontainers.MsSql;

namespace ClaimTrack.IntegrationTests.Infrastructure;

// Runs the real API in memory against a throwaway SQL Server container.
// One instance is shared by every test in a run (see ApiCollection).
public class ClaimTrackApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // "Today" for every test, so date rules give the same result whenever the tests run.
    public static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    // Users created once per run, so most tests can act as "a policyholder" or "a supervisor"
    // without creating their own.
    public static readonly Guid PolicyholderId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid SupervisorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public const string TestSigningKey = "test-only-signing-key-not-a-secret-0123456789";

    private readonly MsSqlContainer _database =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string DatabaseConnectionString => _database.GetConnectionString();

    // Everything the API logs during the run, for tests that check logging.
    public CollectingLogSink LogSink { get; } = new();

    // Uploaded documents go to a throwaway folder for this run, deleted afterwards.
    public string DocumentStoragePath { get; } =
        Path.Combine(Path.GetTempPath(), $"claimtrack-test-documents-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development", so user-secrets aren't loaded: if the override below ever failed,
        // the API would have no connection string and fail, instead of using your real database.
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ClaimTrack"] = DatabaseConnectionString,
                // A fake key that only signs tokens inside this test run. The real key is in user-secrets.
                ["Jwt:SigningKey"] = TestSigningKey,
                // The tests log in and call the API hundreds of times from one "client", so the real
                // limits would block them. RateLimitingTests uses a copy of the API with small limits.
                ["RateLimiting:Auth:PermitLimit"] = "100000",
                ["RateLimiting:General:PermitLimit"] = "100000",
                // Off so it can't flag shared test data at random moments; OverdueClaimsTests turns it on.
                ["OverdueClaims:Enabled"] = "false",
                ["Documents:StoragePath"] = DocumentStoragePath
            }));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
            services.AddSingleton<ILogEventSink>(LogSink);
        });
    }

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        await CreateUserAsync(Role.Policyholder, id: PolicyholderId);
        await CreateUserAsync(Role.Supervisor, id: SupervisorId);
    }

    public HttpClient CreatePolicyholderClient() => CreateClientAs(PolicyholderId, Role.Policyholder);
    public HttpClient CreateSupervisorClient() => CreateClientAs(SupervisorId, Role.Supervisor);

    // A client whose requests carry "Authorization: Bearer <token>" for this user.
    public HttpClient CreateClientAs(Guid userId, Role role)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(userId, role));
        return client;
    }

    // Creates a new user with the given role and returns a client logged in as them.
    public async Task<HttpClient> CreateClientForNewUserAsync(Role role) =>
        CreateClientAs(await CreateUserAsync(role), role);

    // Signs a token with the API's own TokenService, so it's exactly what login would return.
    // Skips the password check, which AuthEndpointsTests covers.
    public string CreateToken(Guid userId, Role role)
    {
        using var scope = Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        return tokenService.CreateToken(new User { Id = userId, Email = $"{userId}@test.local", Role = role }).AccessToken;
    }

    // Puts a claim into any status directly. Used to set up states the workflow
    // endpoints can't reach yet, or that would take several calls to reach.
    public async Task SetClaimStatusAsync(Guid claimId, ClaimStatus status)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Claims
            .Where(c => c.Id == claimId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.Status, status));
    }

    // Inserts a user straight into the test database, faster than going through register.
    // The password hash is a placeholder because tests log in with CreateToken instead.
    public async Task<Guid> CreateUserAsync(Role role, string? email = null, Guid? id = null, string? fullName = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = new User
        {
            Id = id ?? Guid.NewGuid(),
            Email = email ?? $"{Guid.NewGuid()}@test.local",
            PasswordHash = "not-a-real-hash",
            FullName = fullName ?? $"Test {role}",
            Role = role,
            CreatedAt = Now
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user.Id;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _database.DisposeAsync();
        if (Directory.Exists(DocumentStoragePath))
        {
            Directory.Delete(DocumentStoragePath, recursive: true);
        }
    }
}
