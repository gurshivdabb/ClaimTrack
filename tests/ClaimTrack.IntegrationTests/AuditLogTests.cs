using System.Net;
using AwesomeAssertions;
using ClaimTrack.Api.Data;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Enums;
using ClaimTrack.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimTrack.IntegrationTests;

// BR-07 and BR-08. The history endpoint comes in the next task, so these read the table directly.
[Collection(ApiCollection.Name)]
public class AuditLogTests(ClaimTrackApiFactory factory)
{
    private const string DenialReason = "Damage happened before the policy started.";

    private readonly HttpClient _supervisor = factory.CreateSupervisorClient();
    private readonly HttpClient _policyholder = factory.CreatePolicyholderClient();

    // The test clock never moves, so every entry has the same Timestamp. EF gives SQL Server
    // keys sequential GUIDs, so Id is the tie-breaker that keeps insertion order.
    private async Task<List<AuditLogEntry>> GetAuditLogAsync(Guid claimId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.AuditLog.AsNoTracking()
            .Where(a => a.ClaimId == claimId)
            .OrderBy(a => a.Timestamp).ThenBy(a => a.Id)
            .ToListAsync();
    }

    private async Task<ClaimStatus> GetStatusAsync(Guid claimId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Claims.Where(c => c.Id == claimId).Select(c => c.Status).SingleAsync();
    }

    [Fact]
    public async Task Submit_RecordsSubmittedEntry()
    {
        var claim = await factory.SubmitClaimAsync();

        var entry = (await GetAuditLogAsync(claim.Id)).Should().ContainSingle().Subject;
        entry.Action.Should().Be(AuditAction.Submitted);
        entry.ActorUserId.Should().Be(ClaimTrackApiFactory.PolicyholderId);
        entry.FromStatus.Should().BeNull();
        entry.ToStatus.Should().Be(ClaimStatus.Submitted);
        entry.Timestamp.Should().Be(ClaimTrackApiFactory.Now);
    }

    [Fact]
    public async Task FullJourney_RecordsEveryStepWithActorAndStatuses()
    {
        var (claim, adjusterId, _) = await factory.ReadyForDecisionAsync(Recommendation.Approve);
        await _supervisor.DecideAsync(claim.Id, ClaimDecision.Approve);

        var log = await GetAuditLogAsync(claim.Id);

        log.Select(e => (e.Action, e.ActorUserId, e.FromStatus, e.ToStatus, e.Details)).Should().Equal(
            (AuditAction.Submitted, ClaimTrackApiFactory.PolicyholderId, null, ClaimStatus.Submitted, null),
            (AuditAction.Assigned, ClaimTrackApiFactory.SupervisorId, null, null, "Assigned to Test Adjuster"),
            (AuditAction.ReviewStarted, adjusterId, ClaimStatus.Submitted, ClaimStatus.UnderReview, null),
            (AuditAction.Recommended, adjusterId, null, null, "Recommended Approve"),
            (AuditAction.Approved, ClaimTrackApiFactory.SupervisorId, ClaimStatus.UnderReview, ClaimStatus.Approved, null));
    }

    [Fact]
    public async Task Deny_RecordsTheReason()
    {
        var (claim, _, _) = await factory.ReadyForDecisionAsync();

        await _supervisor.DecideAsync(claim.Id, ClaimDecision.Deny, DenialReason);

        var entry = (await GetAuditLogAsync(claim.Id)).Last();
        entry.Action.Should().Be(AuditAction.Denied);
        entry.ToStatus.Should().Be(ClaimStatus.Denied);
        entry.Details.Should().Be(DenialReason);
    }

    [Fact]
    public async Task Withdraw_RecordsWithdrawnEntry()
    {
        var claim = await factory.SubmitClaimAsync();

        await _policyholder.WithdrawAsync(claim.Id);

        var entry = (await GetAuditLogAsync(claim.Id)).Last();
        entry.Action.Should().Be(AuditAction.Withdrawn);
        entry.ActorUserId.Should().Be(ClaimTrackApiFactory.PolicyholderId);
        (entry.FromStatus, entry.ToStatus).Should().Be((ClaimStatus.Submitted, ClaimStatus.Withdrawn));
    }

    [Fact]
    public async Task Update_RecordsWhatChanged()
    {
        var claim = await factory.SubmitClaimAsync();
        var update = new UpdateClaimRequest(claim.IncidentDate, "Rear-ended, bumper and trunk", 1_500m);

        await _policyholder.PutJsonAsync($"/api/claims/{claim.Id}", update);

        var entry = (await GetAuditLogAsync(claim.Id)).Last();
        entry.Action.Should().Be(AuditAction.Updated);
        entry.ToStatus.Should().BeNull(); // an edit isn't a status change
        entry.Details.Should().Be("ClaimedAmount 1000.00 → 1500.00; Description changed");
    }

    [Fact]
    public async Task RejectedActions_RecordNothing()
    {
        var (claim, _, adjuster) = await factory.SubmitAndAssignAsync();
        await adjuster.StartReviewAsync(claim.Id);
        var before = await GetAuditLogAsync(claim.Id);

        (await adjuster.StartReviewAsync(claim.Id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _supervisor.DecideAsync(claim.Id, ClaimDecision.Approve)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _policyholder.WithdrawAsync(claim.Id)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await GetAuditLogAsync(claim.Id)).Should().HaveCount(before.Count);
    }

    // BR-07: the change and its audit entry are saved together, or not at all. A supervisor
    // token for a user who isn't in the database makes the audit row's actor foreign key fail.
    [Fact]
    public async Task FailedAuditWrite_AlsoUndoesTheStatusChange()
    {
        var (claim, _, _) = await factory.ReadyForDecisionAsync();
        var ghostSupervisor = factory.CreateClientAs(Guid.NewGuid(), Role.Supervisor);
        var entriesBefore = await GetAuditLogAsync(claim.Id);

        var response = await ghostSupervisor.DecideAsync(claim.Id, ClaimDecision.Approve);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await GetStatusAsync(claim.Id)).Should().Be(ClaimStatus.UnderReview);
        (await GetAuditLogAsync(claim.Id)).Should().HaveCount(entriesBefore.Count);
    }

    // BR-08: the safety net in AppDbContext refuses to change or remove history.
    [Fact]
    public async Task ModifyingAnAuditEntry_Throws()
    {
        var claim = await factory.SubmitClaimAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entry = await db.AuditLog.SingleAsync(a => a.ClaimId == claim.Id);

        entry.Details = "Rewriting history";
        var save = () => db.SaveChangesAsync();

        await save.Should().ThrowAsync<InvalidOperationException>().WithMessage("*append-only*");
    }

    [Fact]
    public async Task DeletingAnAuditEntry_Throws()
    {
        var claim = await factory.SubmitClaimAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entry = await db.AuditLog.SingleAsync(a => a.ClaimId == claim.Id);

        db.AuditLog.Remove(entry);
        var save = () => db.SaveChangesAsync();

        await save.Should().ThrowAsync<InvalidOperationException>().WithMessage("*append-only*");
        (await GetAuditLogAsync(claim.Id)).Should().ContainSingle();
    }
}
