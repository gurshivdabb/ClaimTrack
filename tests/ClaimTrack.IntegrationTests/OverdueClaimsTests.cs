using System.Net;
using AwesomeAssertions;
using ClaimTrack.Api.Common;
using ClaimTrack.Api.Data;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Enums;
using ClaimTrack.Api.Services;
using ClaimTrack.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Serilog.Events;
using static ClaimTrack.IntegrationTests.Infrastructure.ApiClientExtensions;

namespace ClaimTrack.IntegrationTests;

// Claims stuck UnderReview for 14+ days get flagged. Reviews in these tests start at the test
// clock's "now", so running the check with a clock N days later makes them N days old.
[Collection(ApiCollection.Name)]
public class OverdueClaimsTests(ClaimTrackApiFactory factory)
{
    private static readonly DateTimeOffset Now = ClaimTrackApiFactory.Now;

    private readonly HttpClient _supervisor = factory.CreateSupervisorClient();

    private async Task<int> RunCheckAsync(int daysLater)
    {
        using var scope = factory.Services.CreateScope();
        var service = new OverdueClaimsService(
            scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            Options.Create(new OverdueClaimsOptions()),
            new FakeTimeProvider(Now.AddDays(daysLater)));
        return await service.FlagOverdueClaimsAsync(CancellationToken.None);
    }

    private async Task<DateTimeOffset?> FlaggedAtAsync(Guid claimId) =>
        (await (await _supervisor.GetAsync($"/api/claims/{claimId}")).ReadAsync<ClaimResponse>()).FlaggedOverdueAt;

    private async Task<Guid> ClaimUnderReviewAsync()
    {
        var (claim, _, adjuster) = await factory.SubmitAndAssignAsync();
        (await adjuster.StartReviewAsync(claim.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        return claim.Id;
    }

    [Fact]
    public async Task Check_Reviewed15DaysAgoWithRecommendation_IsFlagged()
    {
        // A recommendation doesn't reset the clock: the claim still waits on the supervisor.
        var (claim, _, _) = await factory.ReadyForDecisionAsync();

        await RunCheckAsync(daysLater: 15);

        (await FlaggedAtAsync(claim.Id)).Should().Be(Now.AddDays(15));
    }

    [Theory]
    [InlineData(13, false)]
    [InlineData(14, true)] // the boundary: exactly 14 days counts as overdue
    public async Task Check_AroundTheThreshold_FlagsFrom14Days(int daysLater, bool expectFlagged)
    {
        var claimId = await ClaimUnderReviewAsync();

        await RunCheckAsync(daysLater);

        (await FlaggedAtAsync(claimId) is not null).Should().Be(expectFlagged);
    }

    [Fact]
    public async Task Check_RunTwice_KeepsTheFirstFlagTime()
    {
        var claimId = await ClaimUnderReviewAsync();

        await RunCheckAsync(daysLater: 15);
        await RunCheckAsync(daysLater: 16);

        (await FlaggedAtAsync(claimId)).Should().Be(Now.AddDays(15));
    }

    [Fact]
    public async Task Check_SubmittedOrDecidedClaims_AreNeverFlagged()
    {
        var submitted = await factory.SubmitClaimAsync();
        var (decided, _, _) = await factory.ReadyForDecisionAsync();
        await _supervisor.DecideAsync(decided.Id, ClaimDecision.Approve);

        await RunCheckAsync(daysLater: 30);

        (await FlaggedAtAsync(submitted.Id)).Should().BeNull();
        (await FlaggedAtAsync(decided.Id)).Should().BeNull();
    }

    [Fact]
    public async Task List_OverdueFilter_ReturnsOnlyFlaggedClaimsTheUserCanSee()
    {
        // A fresh policyholder with one claim under review, plus another holder with none.
        var holderId = await factory.CreateUserAsync(Role.Policyholder);
        var holder = factory.CreateClientAs(holderId, Role.Policyholder);
        var policy = await _supervisor.CreatePolicyAsync(ValidPolicyRequest() with { HolderId = holderId });
        var claim = await holder.SubmitClaimAsync(policy.Id);
        var adjusterId = await factory.CreateUserAsync(Role.Adjuster);
        await _supervisor.AssignAsync(claim.Id, adjusterId);
        await factory.CreateClientAs(adjusterId, Role.Adjuster).StartReviewAsync(claim.Id);
        var otherHolder = await factory.CreateClientForNewUserAsync(Role.Policyholder);

        await RunCheckAsync(daysLater: 15);

        var overdue = await (await holder.GetAsync("/api/claims?overdue=true")).ReadAsync<PagedResult<ClaimResponse>>();
        var notOverdue = await (await holder.GetAsync("/api/claims?overdue=false")).ReadAsync<PagedResult<ClaimResponse>>();
        var othersView = await (await otherHolder.GetAsync("/api/claims?overdue=true")).ReadAsync<PagedResult<ClaimResponse>>();

        overdue.Items.Select(c => c.Id).Should().Equal(claim.Id);
        notOverdue.TotalCount.Should().Be(0);
        othersView.TotalCount.Should().Be(0); // BR-09: flagged claims of other holders stay invisible
    }

    // The background job itself: switched on in a copy of the API whose clock is 20 days ahead,
    // it flags the claim at startup without anyone calling an endpoint.
    [Fact]
    public async Task Job_WhenEnabled_FlagsOverdueClaimsOnItsOwn()
    {
        var claimId = await ClaimUnderReviewAsync();

        using var api = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["OverdueClaims:Enabled"] = "true" }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now.AddDays(20)));
            });
        });
        api.CreateClient(); // starts the API, and with it the job

        DateTimeOffset? flaggedAt = null;
        for (var attempt = 0; attempt < 50 && flaggedAt is null; attempt++)
        {
            await Task.Delay(100);
            flaggedAt = await ReadFlagFromDatabaseAsync(claimId);
        }
        flaggedAt.Should().Be(Now.AddDays(20));
    }

    [Fact]
    public async Task Job_WhenACheckFails_LogsTheErrorAndTheApiKeepsRunning()
    {
        using var api = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["OverdueClaims:Enabled"] = "true" }));
            builder.ConfigureTestServices(services => services.AddScoped<IOverdueClaimsService, FailingOverdueClaimsService>());
        });
        var client = api.CreateClient();

        LogEvent? error = null;
        for (var attempt = 0; attempt < 50 && error is null; attempt++)
        {
            await Task.Delay(100);
            error = factory.LogSink.Events.FirstOrDefault(e =>
                e.Level == LogEventLevel.Error && e.Exception?.Message == FailingOverdueClaimsService.Message);
        }

        error.Should().NotBeNull("the failed run should be logged");
        (await client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK); // still serving
    }

    private async Task<DateTimeOffset?> ReadFlagFromDatabaseAsync(Guid claimId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Claims.Where(c => c.Id == claimId).Select(c => c.FlaggedOverdueAt).SingleAsync();
    }

    private sealed class FailingOverdueClaimsService : IOverdueClaimsService
    {
        public const string Message = "Simulated failure in the overdue-claims check";

        public Task<int> FlagOverdueClaimsAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException(Message);
    }
}
