using System.Net;
using AwesomeAssertions;
using ClaimTrack.Api.Common;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Dtos.Policies;
using ClaimTrack.Api.Enums;
using ClaimTrack.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using static ClaimTrack.IntegrationTests.Infrastructure.ApiClientExtensions;

namespace ClaimTrack.IntegrationTests;

// BR-09: users only see what their role allows; anything else behaves as if it doesn't exist.
// Every test makes its own policyholders A and B, so "someone else's data" is real data.
[Collection(ApiCollection.Name)]
public class VisibilityTests(ClaimTrackApiFactory factory)
{
    private readonly HttpClient _supervisor = factory.CreateSupervisorClient();

    private sealed record Holder(Guid Id, HttpClient Client, PolicyResponse Policy, ClaimResponse Claim);

    // A new policyholder with one policy and one claim on it.
    private async Task<Holder> CreateHolderWithClaimAsync()
    {
        var id = await factory.CreateUserAsync(Role.Policyholder);
        var client = factory.CreateClientAs(id, Role.Policyholder);
        var policy = await _supervisor.CreatePolicyAsync(ValidPolicyRequest() with { HolderId = id });
        var claim = await client.SubmitClaimAsync(policy.Id);
        return new Holder(id, client, policy, claim);
    }

    // Uses the real assign endpoint, as a supervisor would.
    private async Task AssignAsync(Guid claimId, Guid adjusterId)
    {
        var response = await _supervisor.AssignAsync(claimId, adjusterId);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Policyholder_ListsOnlyShowTheirOwnPoliciesAndClaims()
    {
        var alice = await CreateHolderWithClaimAsync();
        var bob = await CreateHolderWithClaimAsync();

        var policies = await (await alice.Client.GetAsync("/api/policies")).ReadAsync<List<PolicyResponse>>();
        var claims = (await (await alice.Client.GetAsync("/api/claims")).ReadAsync<PagedResult<ClaimResponse>>()).Items;

        policies.Select(p => p.Id).Should().Equal(alice.Policy.Id);
        claims.Select(c => c.Id).Should().Equal(alice.Claim.Id);
        policies.Should().NotContain(p => p.Id == bob.Policy.Id);
    }

    [Fact]
    public async Task Policyholder_OtherHoldersPolicyAndClaim_Return404()
    {
        var alice = await CreateHolderWithClaimAsync();
        var bob = await CreateHolderWithClaimAsync();
        var update = new UpdateClaimRequest(new DateOnly(2026, 6, 2), "Trying to edit Bob's claim", 1m);

        (await alice.Client.GetAsync($"/api/policies/{bob.Policy.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await alice.Client.GetAsync($"/api/claims/{bob.Claim.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await alice.Client.PutJsonAsync($"/api/claims/{bob.Claim.Id}", update)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SubmitClaim_OnAnotherHoldersPolicy_Returns422PolicyNotFound()
    {
        var alice = await CreateHolderWithClaimAsync();
        var bob = await CreateHolderWithClaimAsync();

        var response = await alice.Client.PostJsonAsync("/api/claims", ValidClaimRequest(bob.Policy.Id));

        // BR-03, with the same message as a policy that doesn't exist (BR-09).
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.ReadAsync<ProblemDetails>()).Detail.Should().Be("Policy not found.");
    }

    [Fact]
    public async Task Adjuster_SeesClaimAndItsPolicyOnlyOnceAssignedToThem()
    {
        var alice = await CreateHolderWithClaimAsync();
        var adjusterId = await factory.CreateUserAsync(Role.Adjuster);
        var adjuster = factory.CreateClientAs(adjusterId, Role.Adjuster);

        (await adjuster.GetAsync($"/api/claims/{alice.Claim.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await adjuster.GetAsync($"/api/policies/{alice.Policy.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        await AssignAsync(alice.Claim.Id, adjusterId);

        (await adjuster.GetAsync($"/api/claims/{alice.Claim.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await adjuster.GetAsync($"/api/policies/{alice.Policy.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        var claims = (await (await adjuster.GetAsync("/api/claims")).ReadAsync<PagedResult<ClaimResponse>>()).Items;
        var policies = await (await adjuster.GetAsync("/api/policies")).ReadAsync<List<PolicyResponse>>();
        claims.Select(c => c.Id).Should().Equal(alice.Claim.Id);
        policies.Select(p => p.Id).Should().Equal(alice.Policy.Id);
    }

    [Fact]
    public async Task Adjuster_ClaimAssignedToAnotherAdjuster_Returns404()
    {
        var alice = await CreateHolderWithClaimAsync();
        await AssignAsync(alice.Claim.Id, await factory.CreateUserAsync(Role.Adjuster));
        var otherAdjuster = await factory.CreateClientForNewUserAsync(Role.Adjuster);

        (await otherAdjuster.GetAsync($"/api/claims/{alice.Claim.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await otherAdjuster.GetAsync($"/api/policies/{alice.Policy.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Supervisor_SeesEveryonesPoliciesAndClaims()
    {
        var alice = await CreateHolderWithClaimAsync();
        var bob = await CreateHolderWithClaimAsync();

        var policies = await (await _supervisor.GetAsync("/api/policies")).ReadAsync<List<PolicyResponse>>();
        var claims = (await (await _supervisor.GetAsync("/api/claims")).ReadAsync<PagedResult<ClaimResponse>>()).Items;

        policies.Select(p => p.Id).Should().Contain([alice.Policy.Id, bob.Policy.Id]);
        claims.Select(c => c.Id).Should().Contain([alice.Claim.Id, bob.Claim.Id]);
        (await _supervisor.GetAsync($"/api/claims/{bob.Claim.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
