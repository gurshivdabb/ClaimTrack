using System.Net;
using AwesomeAssertions;
using ClaimTrack.Api.Dtos.Policies;
using ClaimTrack.Api.Enums;
using ClaimTrack.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using static ClaimTrack.IntegrationTests.Infrastructure.ApiClientExtensions;

namespace ClaimTrack.IntegrationTests;

[Collection(ApiCollection.Name)]
public class PoliciesEndpointsTests(ClaimTrackApiFactory factory)
{
    // Supervisors manage policies; a policyholder submits the claim in the delete-with-claims test.
    private readonly HttpClient _client = factory.CreateSupervisorClient();
    private readonly HttpClient _policyholder = factory.CreatePolicyholderClient();

    [Fact]
    public async Task Create_ValidPolicy_Returns201WithLocationAndPolicyNumber()
    {
        var response = await _client.PostJsonAsync("/api/policies", ValidPolicyRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var policy = await response.ReadAsync<PolicyResponse>();
        response.Headers.Location!.AbsolutePath.Should().Be($"/api/policies/{policy.Id}");
        policy.PolicyNumber.Should().MatchRegex(@"^POL-2026-\d{5}$"); // BR-10
        policy.CreatedAt.Should().Be(ClaimTrackApiFactory.Now);
    }

    [Fact]
    public async Task Create_InvalidPolicy_Returns400WithCamelCaseFieldNames()
    {
        var request = ValidPolicyRequest() with
        {
            HolderId = Guid.Empty,
            CoverageLimit = 0m,
            EndDate = new DateOnly(2025, 12, 31)
        };

        var response = await _client.PostJsonAsync("/api/policies", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.ReadAsync<ValidationProblemDetails>();
        problem.Errors.Keys.Should().BeEquivalentTo("holderId", "coverageLimit", "endDate");
    }

    [Fact]
    public async Task Create_UnknownHolder_Returns422()
    {
        var request = ValidPolicyRequest() with { HolderId = Guid.NewGuid() };

        var response = await _client.PostJsonAsync("/api/policies", request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await response.ReadAsync<ProblemDetails>();
        problem.Detail.Should().Be("Holder must be an existing policyholder.");
    }

    [Fact]
    public async Task Create_HolderIsAnAdjuster_Returns422()
    {
        var adjusterId = await factory.CreateUserAsync(Role.Adjuster);
        var request = ValidPolicyRequest() with { HolderId = adjusterId };

        var response = await _client.PostJsonAsync("/api/policies", request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GetAll_AfterCreatingPolicy_ContainsIt()
    {
        var created = await _client.CreatePolicyAsync();

        var response = await _client.GetAsync("/api/policies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var policies = await response.ReadAsync<List<PolicyResponse>>();
        policies.Should().ContainEquivalentOf(created);
    }

    [Fact]
    public async Task GetById_ExistingPolicy_Returns200()
    {
        var created = await _client.CreatePolicyAsync();

        var response = await _client.GetAsync($"/api/policies/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadAsync<PolicyResponse>()).Should().BeEquivalentTo(created);
    }

    [Fact]
    public async Task GetById_UnknownPolicy_Returns404ProblemDetails()
    {
        var response = await _client.GetAsync($"/api/policies/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Update_ExistingPolicy_Returns200WithChanges()
    {
        var created = await _client.CreatePolicyAsync();
        var update = new UpdatePolicyRequest(
            CoverageType.Home, 300_000m, 1_000m, created.StartDate, created.EndDate);

        var response = await _client.PutJsonAsync($"/api/policies/{created.Id}", update);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.ReadAsync<PolicyResponse>();
        updated.CoverageType.Should().Be(CoverageType.Home);
        updated.CoverageLimit.Should().Be(300_000m);
        updated.PolicyNumber.Should().Be(created.PolicyNumber);
    }

    [Fact]
    public async Task Update_InvalidPolicy_Returns400()
    {
        var created = await _client.CreatePolicyAsync();
        var update = new UpdatePolicyRequest(
            CoverageType.Home, 300_000m, -1m, created.StartDate, created.EndDate);

        var response = await _client.PutJsonAsync($"/api/policies/{created.Id}", update);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_UnknownPolicy_Returns404()
    {
        var update = new UpdatePolicyRequest(
            CoverageType.Home, 1_000m, 0m, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        var response = await _client.PutJsonAsync($"/api/policies/{Guid.NewGuid()}", update);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_PolicyWithoutClaims_Returns204AndRemovesIt()
    {
        var created = await _client.CreatePolicyAsync();

        var response = await _client.DeleteAsync($"/api/policies/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.GetAsync($"/api/policies/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_PolicyWithClaims_Returns409()
    {
        var policy = await _client.CreatePolicyAsync();
        await _policyholder.SubmitClaimAsync(policy.Id);

        var response = await _client.DeleteAsync($"/api/policies/{policy.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.ReadAsync<ProblemDetails>();
        problem.Detail.Should().Be("This policy has claims and cannot be deleted.");
    }

    [Fact]
    public async Task Delete_UnknownPolicy_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/policies/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
