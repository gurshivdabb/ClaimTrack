using System.Net;
using AwesomeAssertions;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Enums;

namespace ClaimTrack.IntegrationTests.Infrastructure;

// A claim at a given point in its lifecycle, plus the adjuster it's assigned to.
public sealed record AssignedClaim(ClaimResponse Claim, Guid AdjusterId, HttpClient Adjuster);

// Gets a claim to a given point in the workflow through the real endpoints, so tests only
// spell out the step they're actually testing. Claims belong to the shared test policyholder.
public static class ClaimScenarios
{
    public static async Task<ClaimResponse> SubmitClaimAsync(this ClaimTrackApiFactory factory)
    {
        var policy = await factory.CreateSupervisorClient().CreatePolicyAsync();
        return await factory.CreatePolicyholderClient().SubmitClaimAsync(policy.Id);
    }

    // Submitted and assigned to a new adjuster.
    public static async Task<AssignedClaim> SubmitAndAssignAsync(this ClaimTrackApiFactory factory)
    {
        var claim = await factory.SubmitClaimAsync();
        var adjusterId = await factory.CreateUserAsync(Role.Adjuster);

        var response = await factory.CreateSupervisorClient().AssignAsync(claim.Id, adjusterId);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return new AssignedClaim(claim, adjusterId, factory.CreateClientAs(adjusterId, Role.Adjuster));
    }

    // Assigned, under review, and recommended: ready for the supervisor's decision.
    public static async Task<AssignedClaim> ReadyForDecisionAsync(
        this ClaimTrackApiFactory factory, Recommendation recommendation = Recommendation.Approve)
    {
        var assigned = await factory.SubmitAndAssignAsync();
        (await assigned.Adjuster.StartReviewAsync(assigned.Claim.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await assigned.Adjuster.RecommendAsync(assigned.Claim.Id, recommendation)).StatusCode.Should().Be(HttpStatusCode.OK);
        return assigned;
    }
}
