using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using ClaimTrack.Api.Common;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Dtos.Policies;
using ClaimTrack.Api.Enums;

namespace ClaimTrack.IntegrationTests.Infrastructure;

// Shared helpers so each test only spells out what it's actually testing.
// Every test creates its own policy, so tests never depend on each other's data.
public static class ApiClientExtensions
{
    // The same JSON rules as the API (camelCase, enums as text), from the API's own JsonSettings.
    public static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        JsonSettings.Apply(options);
        return options;
    }

    public static CreatePolicyRequest ValidPolicyRequest() => new(
        HolderId: ClaimTrackApiFactory.PolicyholderId,
        CoverageType.Auto,
        CoverageLimit: 5_000m,
        Deductible: 250m,
        StartDate: new DateOnly(2026, 1, 1),
        EndDate: new DateOnly(2026, 12, 31));

    public static SubmitClaimRequest ValidClaimRequest(Guid policyId) => new(
        policyId,
        IncidentDate: new DateOnly(2026, 6, 1),
        Description: "Rear-ended at a red light",
        ClaimedAmount: 1_000m);

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, Json);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PutAsJsonAsync(url, body, Json);

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    public static async Task<PolicyResponse> CreatePolicyAsync(this HttpClient client, CreatePolicyRequest? request = null)
    {
        var response = await client.PostJsonAsync("/api/policies", request ?? ValidPolicyRequest());
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.ReadAsync<PolicyResponse>();
    }

    public static async Task<ClaimResponse> SubmitClaimAsync(this HttpClient client, Guid policyId)
    {
        var response = await client.PostJsonAsync("/api/claims", ValidClaimRequest(policyId));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.ReadAsync<ClaimResponse>();
    }

    // Workflow calls. Each takes the client of the role that's allowed to make it.
    public const string RecommendationNotes = "Photos and repair quote match the incident.";

    public static Task<HttpResponseMessage> AssignAsync(this HttpClient supervisor, Guid claimId, Guid adjusterId) =>
        supervisor.PostJsonAsync($"/api/claims/{claimId}/assign", new AssignClaimRequest(adjusterId));

    public static Task<HttpResponseMessage> StartReviewAsync(this HttpClient adjuster, Guid claimId) =>
        adjuster.PostAsync($"/api/claims/{claimId}/start-review", null);

    public static Task<HttpResponseMessage> RecommendAsync(
        this HttpClient adjuster, Guid claimId, Recommendation recommendation = Recommendation.Approve) =>
        adjuster.PostJsonAsync($"/api/claims/{claimId}/recommendation",
            new RecommendClaimRequest(recommendation, RecommendationNotes));

    public static Task<HttpResponseMessage> DecideAsync(
        this HttpClient supervisor, Guid claimId, ClaimDecision decision, string? reason = null) =>
        supervisor.PostJsonAsync($"/api/claims/{claimId}/decision", new DecideClaimRequest(decision, reason));

    public static Task<HttpResponseMessage> WithdrawAsync(this HttpClient policyholder, Guid claimId) =>
        policyholder.PostAsync($"/api/claims/{claimId}/withdraw", null);
}
