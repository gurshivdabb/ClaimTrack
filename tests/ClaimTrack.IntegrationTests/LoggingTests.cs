using System.Net;
using AwesomeAssertions;
using ClaimTrack.Api.Common;
using ClaimTrack.Api.Dtos.Policies;
using ClaimTrack.Api.Enums;
using ClaimTrack.Api.Services;
using ClaimTrack.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;

namespace ClaimTrack.IntegrationTests;

// What the API writes to its logs, read from the in-memory sink the test factory registers.
[Collection(ApiCollection.Name)]
public class LoggingTests(ClaimTrackApiFactory factory)
{
    private const string RequestLogSource = "Serilog.AspNetCore.RequestLoggingMiddleware";

    private static string? Text(LogEvent logEvent, string property) =>
        logEvent.Properties.TryGetValue(property, out var value) && value is ScalarValue { Value: { } raw }
            ? raw.ToString()
            : null;

    private List<LogEvent> RequestEventsFor(Guid userId) =>
        factory.LogSink.Events.Where(e => Text(e, "UserId") == userId.ToString()).ToList();

    [Fact]
    public async Task Request_LoggedIn_LogsOneSummaryWithUserRoleAndStatus()
    {
        var userId = await factory.CreateUserAsync(Role.Policyholder);
        var client = factory.CreateClientAs(userId, Role.Policyholder);

        await client.GetAsync("/api/claims?status=Submitted");

        var logEvent = await WaitForAsync(() => RequestEventsFor(userId).SingleOrDefault());
        RequestEventsFor(userId).Should().ContainSingle("one summary line per request");
        logEvent.Level.Should().Be(LogEventLevel.Information);
        Text(logEvent, "RequestMethod").Should().Be("GET");
        Text(logEvent, "RequestPath").Should().Be("/api/claims");
        Text(logEvent, "StatusCode").Should().Be("200");
        Text(logEvent, "UserRole").Should().Be("Policyholder");
    }

    [Fact]
    public async Task Logs_NeverContainTheTokenOrTheQueryString()
    {
        var userId = await factory.CreateUserAsync(Role.Supervisor);
        var token = factory.CreateToken(userId, Role.Supervisor);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        await client.GetAsync("/api/claims?status=Submitted&sort=-claimedAmount");

        await WaitForAsync(() => RequestEventsFor(userId).SingleOrDefault());
        foreach (var logEvent in factory.LogSink.Events)
        {
            var everything = logEvent.RenderMessage() + string.Join(" ", logEvent.Properties.Values);
            everything.Should().NotContain(token).And.NotContain("Bearer ").And.NotContain("sort=-claimedAmount");
        }
    }

    [Fact]
    public async Task Request_Anonymous_LogsStatusButNoUser()
    {
        var path = $"/api/nothing-here-{Guid.NewGuid():N}";

        await factory.CreateClient().GetAsync(path);

        var logEvent = await WaitForAsync(() => factory.LogSink.Events.SingleOrDefault(e => Text(e, "RequestPath") == path));
        Text(logEvent, "StatusCode").Should().Be("404");
        logEvent.Properties.Should().NotContainKey("UserId");
    }

    // A client that hangs up mid-request isn't a server error: no Error log, status 499.
    [Fact]
    public async Task ClientCancelledRequest_IsNotLoggedAsAServerError()
    {
        using var slowApi = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<IPolicyService, SlowPolicyService>()));
        var userId = Guid.NewGuid();
        var client = slowApi.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateToken(userId, Role.Supervisor));
        using var hangUp = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        var call = () => client.GetAsync("/api/policies", hangUp.Token);
        await call.Should().ThrowAsync<OperationCanceledException>();

        var requestLog = await WaitForAsync(() => RequestEventsFor(userId).SingleOrDefault());
        Text(requestLog, "StatusCode").Should().Be("499");
        requestLog.Level.Should().Be(LogEventLevel.Information);
        factory.LogSink.Events.Should().NotContain(e =>
            e.Level >= LogEventLevel.Error && Text(e, "Path") == "/api/policies" && e.Timestamp >= requestLog.Timestamp.AddSeconds(-5));
    }

    // Serilog writes the request line just after the response is sent, so in the in-memory test
    // server the client can read the response a moment before the line exists. (Likewise, the server
    // finishes a cancelled request slightly after the client gives up.)
    private static async Task<LogEvent> WaitForAsync(Func<LogEvent?> find)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (find() is { } found)
            {
                return found;
            }
            await Task.Delay(100);
        }
        throw new TimeoutException("The request was never logged.");
    }

    private sealed class SlowPolicyService : IPolicyService
    {
        public async Task<List<PolicyResponse>> GetAllAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken); // waits until the client hangs up
            return [];
        }

        public Task<Result<PolicyResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<PolicyResponse>> CreateAsync(CreatePolicyRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<PolicyResponse>> UpdateAsync(Guid id, UpdatePolicyRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
