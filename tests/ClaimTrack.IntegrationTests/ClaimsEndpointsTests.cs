using System.Net;
using AwesomeAssertions;
using ClaimTrack.Api.Common;
using ClaimTrack.Api.Data;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Enums;
using ClaimTrack.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ClaimTrack.IntegrationTests.Infrastructure.ApiClientExtensions;

namespace ClaimTrack.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ClaimsEndpointsTests(ClaimTrackApiFactory factory)
{
    // Policyholders submit and update claims; a supervisor creates the policies and deletes claims.
    private readonly HttpClient _client = factory.CreatePolicyholderClient();
    private readonly HttpClient _supervisor = factory.CreateSupervisorClient();

    [Fact]
    public async Task Submit_ValidClaim_Returns201AsSubmittedWithClaimNumber()
    {
        var policy = await _supervisor.CreatePolicyAsync();

        var response = await _client.PostJsonAsync("/api/claims", ValidClaimRequest(policy.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var claim = await response.ReadAsync<ClaimResponse>();
        response.Headers.Location!.AbsolutePath.Should().Be($"/api/claims/{claim.Id}");
        claim.Status.Should().Be(ClaimStatus.Submitted);
        claim.ClaimNumber.Should().MatchRegex(@"^CLM-2026-\d{5}$"); // BR-10
        claim.SubmittedAt.Should().Be(ClaimTrackApiFactory.Now);
    }

    [Fact]
    public async Task Submit_InvalidInput_Returns400WithCamelCaseFieldNames()
    {
        var request = ValidClaimRequest(Guid.Empty) with
        {
            ClaimedAmount = 0m,
            IncidentDate = DateOnly.FromDateTime(ClaimTrackApiFactory.Now.UtcDateTime).AddDays(1), // BR-02
            Description = ""
        };

        var response = await _client.PostJsonAsync("/api/claims", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.ReadAsync<ValidationProblemDetails>();
        problem.Errors.Keys.Should().BeEquivalentTo("policyId", "claimedAmount", "incidentDate", "description");
    }

    [Fact]
    public async Task Submit_AmountWithThreeDecimals_Returns400InsteadOfRoundingIt()
    {
        var policy = await _supervisor.CreatePolicyAsync();
        var request = ValidClaimRequest(policy.Id) with { ClaimedAmount = 100.129m };

        var response = await _client.PostJsonAsync("/api/claims", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.ReadAsync<ValidationProblemDetails>();
        problem.Errors.Keys.Should().BeEquivalentTo("claimedAmount");
    }

    [Fact]
    public async Task Submit_AmountOverCoverageLimit_Returns422()
    {
        var policy = await _supervisor.CreatePolicyAsync();
        var request = ValidClaimRequest(policy.Id) with { ClaimedAmount = 5_000.01m };

        var response = await _client.PostJsonAsync("/api/claims", request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await response.ReadAsync<ProblemDetails>();
        problem.Detail.Should().Be("Claimed amount exceeds the policy's coverage limit of 5000.00."); // BR-01
    }

    [Fact]
    public async Task Submit_IncidentBeforePolicyStart_Returns422()
    {
        var policy = await _supervisor.CreatePolicyAsync();
        var request = ValidClaimRequest(policy.Id) with { IncidentDate = new DateOnly(2025, 12, 31) };

        var response = await _client.PostJsonAsync("/api/claims", request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await response.ReadAsync<ProblemDetails>();
        problem.Detail.Should().Be("Incident date must be between 2026-01-01 and 2026-12-31."); // BR-02
    }

    [Fact]
    public async Task Submit_UnknownPolicy_Returns422()
    {
        var response = await _client.PostJsonAsync("/api/claims", ValidClaimRequest(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await response.ReadAsync<ProblemDetails>();
        problem.Detail.Should().Be("Policy not found.");
    }

    [Fact]
    public async Task GetAll_AfterSubmittingClaim_ContainsIt()
    {
        var policy = await _supervisor.CreatePolicyAsync();
        var claim = await _client.SubmitClaimAsync(policy.Id);

        var response = await _client.GetAsync("/api/claims");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var claims = (await response.ReadAsync<PagedResult<ClaimResponse>>()).Items;
        claims.Should().ContainEquivalentOf(claim);
    }

    [Fact]
    public async Task GetById_ExistingClaim_Returns200()
    {
        var policy = await _supervisor.CreatePolicyAsync();
        var claim = await _client.SubmitClaimAsync(policy.Id);

        var response = await _client.GetAsync($"/api/claims/{claim.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadAsync<ClaimResponse>()).Should().BeEquivalentTo(claim);
    }

    [Fact]
    public async Task GetById_UnknownClaim_Returns404()
    {
        var response = await _client.GetAsync($"/api/claims/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_SubmittedClaim_Returns200WithChanges()
    {
        var policy = await _supervisor.CreatePolicyAsync();
        var claim = await _client.SubmitClaimAsync(policy.Id);
        var update = new UpdateClaimRequest(new DateOnly(2026, 6, 2), "Rear-ended, bumper and trunk", 1_500m);

        var response = await _client.PutJsonAsync($"/api/claims/{claim.Id}", update);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.ReadAsync<ClaimResponse>();
        updated.Description.Should().Be("Rear-ended, bumper and trunk");
        updated.ClaimedAmount.Should().Be(1_500m);
        updated.Status.Should().Be(ClaimStatus.Submitted);
    }

    [Fact]
    public async Task Update_ClaimUnderReview_Returns409()
    {
        var policy = await _supervisor.CreatePolicyAsync();
        var claim = await _client.SubmitClaimAsync(policy.Id);
        await factory.SetClaimStatusAsync(claim.Id, ClaimStatus.UnderReview);
        var update = new UpdateClaimRequest(new DateOnly(2026, 6, 2), "Trying to edit", 1_500m);

        var response = await _client.PutJsonAsync($"/api/claims/{claim.Id}", update);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.ReadAsync<ProblemDetails>();
        problem.Detail.Should().Be("Only submitted claims can be updated. This claim is UnderReview.");
    }

    [Fact]
    public async Task Update_AmountOverCoverageLimit_Returns422()
    {
        var policy = await _supervisor.CreatePolicyAsync();
        var claim = await _client.SubmitClaimAsync(policy.Id);
        var update = new UpdateClaimRequest(new DateOnly(2026, 6, 1), "Rear-ended", 9_000m);

        var response = await _client.PutJsonAsync($"/api/claims/{claim.Id}", update);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Update_UnknownClaim_Returns404()
    {
        var update = new UpdateClaimRequest(new DateOnly(2026, 6, 1), "Rear-ended", 100m);

        var response = await _client.PutJsonAsync($"/api/claims/{Guid.NewGuid()}", update);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // BR-12: claims are never hard-deleted, by anyone. The URL exists (GET, PUT), but DELETE
    // isn't allowed on it, so the honest answer is 405 Method Not Allowed.
    [Theory]
    [InlineData(Role.Policyholder)]
    [InlineData(Role.Adjuster)]
    [InlineData(Role.Supervisor)]
    public async Task Delete_Claim_Returns405AndClaimStillExists(Role role)
    {
        var policy = await _supervisor.CreatePolicyAsync();
        var claim = await _client.SubmitClaimAsync(policy.Id);
        var caller = role switch
        {
            Role.Policyholder => _client, // the claim's owner
            Role.Supervisor => _supervisor,
            _ => await factory.CreateClientForNewUserAsync(role)
        };

        var response = await caller.DeleteAsync($"/api/claims/{claim.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        (await _client.GetAsync($"/api/claims/{claim.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
