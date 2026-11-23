using System.Net;
using AwesomeAssertions;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Enums;
using ClaimTrack.IntegrationTests.Infrastructure;

namespace ClaimTrack.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ClaimHistoryTests(ClaimTrackApiFactory factory)
{
    private readonly HttpClient _supervisor = factory.CreateSupervisorClient();
    private readonly HttpClient _policyholder = factory.CreatePolicyholderClient();

    private static Task<HttpResponseMessage> GetHistoryAsync(HttpClient client, Guid claimId) =>
        client.GetAsync($"/api/claims/{claimId}/history");

    [Fact]
    public async Task History_NewClaim_HasOneSubmittedEntry()
    {
        var claim = await factory.SubmitClaimAsync();

        var response = await GetHistoryAsync(_policyholder, claim.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var entry = (await response.ReadAsync<List<AuditLogEntryResponse>>()).Should().ContainSingle().Subject;
        entry.Action.Should().Be(AuditAction.Submitted);
        entry.ToStatus.Should().Be(ClaimStatus.Submitted);
        entry.ActorName.Should().Be("Test Policyholder");
        entry.ActorRole.Should().Be(Role.Policyholder);
    }

    [Fact]
    public async Task History_FullJourney_ListsEveryStepOldestFirst()
    {
        var (claim, adjusterId, _) = await factory.ReadyForDecisionAsync();
        await _supervisor.DecideAsync(claim.Id, ClaimDecision.Approve);

        var history = await (await GetHistoryAsync(_policyholder, claim.Id)).ReadAsync<List<AuditLogEntryResponse>>();

        history.Select(e => (e.Action, e.ActorRole, e.ActorName, e.FromStatus, e.ToStatus)).Should().Equal(
            (AuditAction.Submitted, Role.Policyholder, "Test Policyholder", null, ClaimStatus.Submitted),
            (AuditAction.Assigned, Role.Supervisor, "Test Supervisor", null, null),
            (AuditAction.ReviewStarted, Role.Adjuster, "Test Adjuster", ClaimStatus.Submitted, ClaimStatus.UnderReview),
            (AuditAction.Recommended, Role.Adjuster, "Test Adjuster", null, null),
            (AuditAction.Approved, Role.Supervisor, "Test Supervisor", ClaimStatus.UnderReview, ClaimStatus.Approved));
        history[2].ActorUserId.Should().Be(adjusterId);
    }

    [Fact]
    public async Task History_OwnerAssignedAdjusterAndSupervisor_AllSeeTheSameEntries()
    {
        var (claim, _, adjuster) = await factory.SubmitAndAssignAsync();

        var asOwner = await (await GetHistoryAsync(_policyholder, claim.Id)).ReadAsync<List<AuditLogEntryResponse>>();
        var asAdjuster = await (await GetHistoryAsync(adjuster, claim.Id)).ReadAsync<List<AuditLogEntryResponse>>();
        var asSupervisor = await (await GetHistoryAsync(_supervisor, claim.Id)).ReadAsync<List<AuditLogEntryResponse>>();

        asOwner.Should().HaveCount(2);
        asAdjuster.Should().Equal(asOwner);
        asSupervisor.Should().Equal(asOwner);
    }

    [Fact]
    public async Task History_OtherPolicyholderOrUnassignedAdjuster_Returns404()
    {
        var (claim, _, _) = await factory.SubmitAndAssignAsync();
        var otherHolder = await factory.CreateClientForNewUserAsync(Role.Policyholder);
        var otherAdjuster = await factory.CreateClientForNewUserAsync(Role.Adjuster);

        (await GetHistoryAsync(otherHolder, claim.Id)).StatusCode.Should().Be(HttpStatusCode.NotFound); // BR-09
        (await GetHistoryAsync(otherAdjuster, claim.Id)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task History_UnknownClaim_Returns404()
    {
        var response = await GetHistoryAsync(_supervisor, Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task History_NoToken_Returns401()
    {
        var claim = await factory.SubmitClaimAsync();

        var response = await GetHistoryAsync(factory.CreateClient(), claim.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task History_Response_HasNoEmailsOrPasswordData()
    {
        var (claim, _, _) = await factory.SubmitAndAssignAsync();

        var body = await (await GetHistoryAsync(_policyholder, claim.Id)).Content.ReadAsStringAsync();

        body.Should().NotContainEquivalentOf("email")
            .And.NotContainEquivalentOf("password")
            .And.NotContain("@test.local");
    }
}
