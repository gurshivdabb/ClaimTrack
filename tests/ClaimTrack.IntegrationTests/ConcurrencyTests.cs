using System.Net;
using AwesomeAssertions;
using ClaimTrack.Api.Data;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Enums;
using ClaimTrack.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimTrack.IntegrationTests;

// Optimistic concurrency on claims (Claim.RowVersion). When two people change the same claim at
// the same moment, only the first change is saved; the second gets 409 and saves nothing, not even
// an audit entry (BR-05, BR-07).
[Collection(ApiCollection.Name)]
public class ConcurrencyTests(ClaimTrackApiFactory factory)
{
    private const string DenialReason = "Damage happened before the policy started.";

    // Each case: someone else changes the claim between the moment the API reads it and the moment
    // it saves. Always the same timing, so the test never depends on luck.
    [Theory]
    [InlineData("edit")]
    [InlineData("assign")]
    [InlineData("recommend")]
    [InlineData("decide")]
    public async Task Action_WhenClaimChangedSinceItWasRead_ReturnsConflictAndSavesNothing(string action)
    {
        var (claimId, userId, role) = await ArrangeAsync(action);
        var before = await SnapshotAsync(claimId);

        using var api = ApiWhereSomeoneElseSavesFirst(claimId);
        var response = await ActAsync(ClientAs(api, userId, role), action, claimId);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.ReadAsync<ProblemDetails>()).Detail.Should().Contain("changed by someone else");
        (await SnapshotAsync(claimId)).Should().Be(before); // the claim and its history are untouched
    }

    // Two real requests at once. However they interleave, exactly one decision is recorded:
    // the loser is stopped either by the RowVersion check or, if it arrives a moment later,
    // by the state machine (the claim is already final).
    [Fact]
    public async Task Decide_TwoSupervisorsAtOnce_RecordsExactlyOneDecision()
    {
        var (claim, _, _) = await factory.ReadyForDecisionAsync();
        var first = factory.CreateSupervisorClient();
        var second = factory.CreateSupervisorClient();

        var responses = await Task.WhenAll(
            first.DecideAsync(claim.Id, ClaimDecision.Approve),
            second.DecideAsync(claim.Id, ClaimDecision.Deny, DenialReason));

        responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
        var decisions = await DecisionEntriesAsync(claim.Id);
        decisions.Should().ContainSingle();
        var winner = await responses.Single(r => r.StatusCode == HttpStatusCode.OK).ReadAsync<ClaimResponse>();
        decisions[0].ToStatus.Should().Be(winner.Status);
    }

    // Gets a claim to the point where `action` is allowed, and returns who may perform it.
    private async Task<(Guid ClaimId, Guid UserId, Role Role)> ArrangeAsync(string action)
    {
        switch (action)
        {
            case "edit":
                return ((await factory.SubmitClaimAsync()).Id, ClaimTrackApiFactory.PolicyholderId, Role.Policyholder);
            case "assign":
                return ((await factory.SubmitClaimAsync()).Id, ClaimTrackApiFactory.SupervisorId, Role.Supervisor);
            case "recommend":
                var assigned = await factory.SubmitAndAssignAsync();
                (await assigned.Adjuster.StartReviewAsync(assigned.Claim.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
                return (assigned.Claim.Id, assigned.AdjusterId, Role.Adjuster);
            case "decide":
                return ((await factory.ReadyForDecisionAsync()).Claim.Id, ClaimTrackApiFactory.SupervisorId, Role.Supervisor);
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    private async Task<HttpResponseMessage> ActAsync(HttpClient client, string action, Guid claimId)
    {
        switch (action)
        {
            case "edit":
                var claim = await (await client.GetAsync($"/api/claims/{claimId}")).ReadAsync<ClaimResponse>();
                return await client.PutJsonAsync($"/api/claims/{claimId}",
                    new UpdateClaimRequest(claim.IncidentDate, "A newer description.", claim.ClaimedAmount));
            case "assign":
                return await client.AssignAsync(claimId, await factory.CreateUserAsync(Role.Adjuster));
            case "recommend":
                return await client.RecommendAsync(claimId, Recommendation.Deny);
            case "decide":
                return await client.DecideAsync(claimId, ClaimDecision.Approve);
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    // A copy of the API whose database context runs SomeoneElseSavesFirst before saving.
    private WebApplicationFactory<Program> ApiWhereSomeoneElseSavesFirst(Guid claimId) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<AppDbContext>(options =>
                options.AddInterceptors(new SomeoneElseSavesFirst(factory.DatabaseConnectionString, claimId)))));

    private HttpClient ClientAs(WebApplicationFactory<Program> api, Guid userId, Role role)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateToken(userId, role));
        return client;
    }

    // Everything the actions above could change, plus how much history the claim has.
    private sealed record ClaimSnapshot(
        ClaimStatus Status,
        string Description,
        Guid? AssignedAdjusterId,
        Recommendation? Recommendation,
        int HistoryEntries);

    private async Task<ClaimSnapshot> SnapshotAsync(Guid claimId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var claim = await db.Claims.AsNoTracking().SingleAsync(c => c.Id == claimId);
        var history = await db.AuditLog.CountAsync(a => a.ClaimId == claimId);
        return new ClaimSnapshot(claim.Status, claim.Description, claim.AssignedAdjusterId, claim.Recommendation, history);
    }

    private async Task<List<AuditLogEntry>> DecisionEntriesAsync(Guid claimId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.AuditLog.AsNoTracking()
            .Where(a => a.ClaimId == claimId && (a.Action == AuditAction.Approved || a.Action == AuditAction.Denied))
            .ToListAsync();
    }

    // Plays "another user": just before the API saves its change to the claim, this updates the
    // same row through a separate connection. The UPDATE changes no values, but SQL Server still
    // gives the row a new RowVersion, which is all it takes for the API's save to be refused.
    private sealed class SomeoneElseSavesFirst(string connectionString, Guid claimId) : SaveChangesInterceptor
    {
        private int _done;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var savingThisClaim = eventData.Context!.ChangeTracker.Entries<Claim>()
                .Any(e => e.State == EntityState.Modified && e.Entity.Id == claimId);

            if (savingThisClaim && Interlocked.Exchange(ref _done, 1) == 0)
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = new SqlCommand("UPDATE Claims SET Description = Description WHERE Id = @id", connection);
                command.Parameters.AddWithValue("@id", claimId);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
