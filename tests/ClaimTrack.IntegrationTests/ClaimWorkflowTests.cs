using System.Net;
using AwesomeAssertions;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Enums;
using ClaimTrack.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using static ClaimTrack.IntegrationTests.Infrastructure.ApiClientExtensions;

namespace ClaimTrack.IntegrationTests;

// The claim lifecycle from requirements §4, through the real endpoints.
[Collection(ApiCollection.Name)]
public class ClaimWorkflowTests(ClaimTrackApiFactory factory)
{
    private const string DenialReason = "Damage happened before the policy started.";

    private readonly HttpClient _supervisor = factory.CreateSupervisorClient();
    private readonly HttpClient _policyholder = factory.CreatePolicyholderClient();

    private static async Task ShouldBeConflictAsync(HttpResponseMessage response, string detail)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.ReadAsync<ProblemDetails>()).Detail.Should().Be(detail);
    }

    [Fact]
    public async Task FullJourney_SubmitAssignReviewRecommendApprove_EndsApproved()
    {
        var (claim, _, adjuster) = await factory.SubmitAndAssignAsync();

        var underReview = await (await adjuster.StartReviewAsync(claim.Id)).ReadAsync<ClaimResponse>();
        underReview.Status.Should().Be(ClaimStatus.UnderReview);

        var recommended = await (await adjuster.RecommendAsync(claim.Id)).ReadAsync<ClaimResponse>();
        recommended.Status.Should().Be(ClaimStatus.UnderReview); // a recommendation isn't a status change
        recommended.Recommendation.Should().Be(Recommendation.Approve);
        recommended.AdjusterNotes.Should().Be(RecommendationNotes);

        var response = await _supervisor.DecideAsync(claim.Id, ClaimDecision.Approve);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var approved = await response.ReadAsync<ClaimResponse>();
        approved.Status.Should().Be(ClaimStatus.Approved);
        approved.DecidedAt.Should().Be(ClaimTrackApiFactory.Now);
        approved.DecisionReason.Should().BeNull();
    }

    [Fact]
    public async Task Decide_DenyWithReason_EndsDeniedAndStoresReason()
    {
        var (claim, _, _) = await factory.ReadyForDecisionAsync();

        // The supervisor may decide against the adjuster's recommendation (Approve here).
        var response = await _supervisor.DecideAsync(claim.Id, ClaimDecision.Deny, DenialReason);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var denied = await response.ReadAsync<ClaimResponse>();
        denied.Status.Should().Be(ClaimStatus.Denied);
        denied.DecisionReason.Should().Be(DenialReason);
        denied.DecidedAt.Should().Be(ClaimTrackApiFactory.Now);
    }

    [Fact]
    public async Task Decide_DenyWithShortReason_Returns400()
    {
        var (claim, _, _) = await factory.ReadyForDecisionAsync();

        var response = await _supervisor.DecideAsync(claim.Id, ClaimDecision.Deny, "Too short");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest); // BR-06
        (await response.ReadAsync<ValidationProblemDetails>()).Errors.Keys.Should().BeEquivalentTo("reason");
    }

    [Fact]
    public async Task Decide_WithoutRecommendation_Returns409()
    {
        var (claim, _, adjuster) = await factory.SubmitAndAssignAsync();
        await adjuster.StartReviewAsync(claim.Id);

        var response = await _supervisor.DecideAsync(claim.Id, ClaimDecision.Approve);

        await ShouldBeConflictAsync(response, "A recommendation is required before a decision.");
    }

    [Fact]
    public async Task Decide_ClaimStillSubmitted_Returns409()
    {
        var claim = await factory.SubmitClaimAsync();

        var response = await _supervisor.DecideAsync(claim.Id, ClaimDecision.Approve);

        await ShouldBeConflictAsync(response, "Cannot move a claim from Submitted to Approved."); // BR-05
    }

    [Fact]
    public async Task Recommend_Twice_LatestReplacesEarlier()
    {
        var (claim, _, adjuster) = await factory.ReadyForDecisionAsync(Recommendation.Approve);

        var response = await adjuster.RecommendAsync(claim.Id, Recommendation.Deny);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadAsync<ClaimResponse>()).Recommendation.Should().Be(Recommendation.Deny);
    }

    [Fact]
    public async Task Recommend_ClaimStillSubmitted_Returns409()
    {
        var (claim, _, adjuster) = await factory.SubmitAndAssignAsync();

        var response = await adjuster.RecommendAsync(claim.Id);

        await ShouldBeConflictAsync(response,
            "Recommendations can only be made while a claim is UnderReview. This claim is Submitted.");
    }

    [Fact]
    public async Task Recommend_AdjusterNotAssigned_Returns404()
    {
        var (claim, _, adjuster) = await factory.SubmitAndAssignAsync();
        await adjuster.StartReviewAsync(claim.Id);
        var otherAdjuster = await factory.CreateClientForNewUserAsync(Role.Adjuster);

        var response = await otherAdjuster.RecommendAsync(claim.Id);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound); // BR-09
    }

    [Fact]
    public async Task Recommend_MissingRecommendationAndNotes_Returns400()
    {
        var (claim, _, adjuster) = await factory.SubmitAndAssignAsync();
        await adjuster.StartReviewAsync(claim.Id);

        var response = await adjuster.PostJsonAsync(
            $"/api/claims/{claim.Id}/recommendation", new RecommendClaimRequest(null, ""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadAsync<ValidationProblemDetails>()).Errors.Keys
            .Should().BeEquivalentTo("recommendation", "notes");
    }

    [Fact]
    public async Task StartReview_Twice_Returns409()
    {
        var (claim, _, adjuster) = await factory.SubmitAndAssignAsync();
        await adjuster.StartReviewAsync(claim.Id);

        var response = await adjuster.StartReviewAsync(claim.Id);

        await ShouldBeConflictAsync(response, "Cannot move a claim from UnderReview to UnderReview.");
    }

    [Fact]
    public async Task StartReview_AdjusterNotAssigned_Returns404()
    {
        var (claim, _, _) = await factory.SubmitAndAssignAsync();
        var otherAdjuster = await factory.CreateClientForNewUserAsync(Role.Adjuster);

        var response = await otherAdjuster.StartReviewAsync(claim.Id);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound); // BR-09
    }

    [Fact]
    public async Task Withdraw_SubmittedClaim_EndsWithdrawn()
    {
        var claim = await factory.SubmitClaimAsync();

        var response = await _policyholder.WithdrawAsync(claim.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadAsync<ClaimResponse>()).Status.Should().Be(ClaimStatus.Withdrawn);
    }

    [Fact]
    public async Task Withdraw_ClaimUnderReview_Returns409()
    {
        var (claim, _, adjuster) = await factory.SubmitAndAssignAsync();
        await adjuster.StartReviewAsync(claim.Id);

        var response = await _policyholder.WithdrawAsync(claim.Id);

        await ShouldBeConflictAsync(response, "Cannot move a claim from UnderReview to Withdrawn.");
    }

    [Fact]
    public async Task Withdraw_SomeoneElsesClaim_Returns404()
    {
        var claim = await factory.SubmitClaimAsync();
        var otherHolder = await factory.CreateClientForNewUserAsync(Role.Policyholder);

        var response = await otherHolder.WithdrawAsync(claim.Id);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound); // BR-09
    }

    // §4: Approved, Denied, and Withdrawn are final. Every workflow action is rejected.
    [Theory]
    [InlineData(ClaimStatus.Approved)]
    [InlineData(ClaimStatus.Denied)]
    [InlineData(ClaimStatus.Withdrawn)]
    public async Task EveryAction_OnFinalClaim_Returns409(ClaimStatus finalStatus)
    {
        var (claim, _, adjuster) = await factory.ReadyForDecisionAsync();
        await factory.SetClaimStatusAsync(claim.Id, finalStatus);

        (await adjuster.StartReviewAsync(claim.Id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await adjuster.RecommendAsync(claim.Id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _supervisor.DecideAsync(claim.Id, ClaimDecision.Approve)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _policyholder.WithdrawAsync(claim.Id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
