using System.Net;
using System.Text;
using AwesomeAssertions;
using ClaimTrack.Api.Common;
using ClaimTrack.Api.Data;
using ClaimTrack.Api.Dtos.Policies;
using ClaimTrack.Api.Enums;
using ClaimTrack.Api.Services;
using ClaimTrack.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ClaimTrack.IntegrationTests.Infrastructure.ApiClientExtensions;

namespace ClaimTrack.IntegrationTests;

// Behaviour that applies to the whole API rather than one endpoint.
[Collection(ApiCollection.Name)]
public class ApiBehaviourTests(ClaimTrackApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateSupervisorClient();

    [Fact]
    public void Database_DuringTests_IsTheTestContainer()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var usedServer = new SqlConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
        var containerServer = new SqlConnectionStringBuilder(factory.DatabaseConnectionString).DataSource;

        // Guards against tests ever writing to the real development database.
        usedServer.Should().Be(containerServer);
    }

    [Fact]
    public async Task UnknownRoute_Returns404ProblemDetails()
    {
        // No token on purpose: unknown URLs must give 404, not 401 (see RequireAuthorization in Program.cs).
        var response = await factory.CreateClient().GetAsync("/api/nothing-here");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task MalformedJson_Returns400()
    {
        using var body = new StringContent("{ this is not json", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/policies", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ASP.NET's own 400s (unreadable input) go through ValidationProblems too: camelCase field
    // names, and no exception types or .NET internals in the response.
    [Fact]
    public async Task UnreadableInput_Returns400WithoutInternalDetails()
    {
        using var badJson = new StringContent("{ \"claimedAmount\": \"lots\" }", Encoding.UTF8, "application/json");

        var bodyResponse = await _client.PostAsync("/api/policies", badJson);
        var queryResponse = await _client.GetAsync("/api/claims?status=Pending");

        foreach (var response in new[] { bodyResponse, queryResponse })
        {
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().NotContain("Exception").And.NotContain("System.").And.NotContain("   at ");
        }
        (await queryResponse.ReadAsync<ValidationProblemDetails>()).Errors.Keys.Should().Equal("status");
    }

    [Fact]
    public async Task CreatePolicy_TenConcurrentRequests_GetDistinctNumbers()
    {
        var requests = Enumerable.Range(0, 10).Select(_ => _client.CreatePolicyAsync(ValidPolicyRequest()));

        var policies = await Task.WhenAll(requests);

        policies.Select(p => p.PolicyNumber).Should().OnlyHaveUniqueItems(); // BR-10
    }

    [Fact]
    public async Task UnexpectedException_Returns500WithoutInternalDetails()
    {
        // A copy of the API whose policy service always throws, to simulate a bug.
        using var failingApi = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddScoped<IPolicyService, ThrowingPolicyService>()));
        var client = failingApi.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", factory.CreateToken(ClaimTrackApiFactory.SupervisorId, Role.Supervisor));

        var response = await client.GetAsync("/api/policies");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("secret internal detail");
        var problem = await response.ReadAsync<ProblemDetails>();
        problem.Title.Should().Be("An unexpected error occurred.");
        problem.Extensions.Should().ContainKey("traceId");
    }

    private sealed class ThrowingPolicyService : IPolicyService
    {
        private static Exception Failure() => new InvalidOperationException("secret internal detail");

        public Task<List<PolicyResponse>> GetAllAsync(CancellationToken cancellationToken) => throw Failure();
        public Task<Result<PolicyResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken) => throw Failure();
        public Task<Result<PolicyResponse>> CreateAsync(CreatePolicyRequest request, CancellationToken cancellationToken) => throw Failure();
        public Task<Result<PolicyResponse>> UpdateAsync(Guid id, UpdatePolicyRequest request, CancellationToken cancellationToken) => throw Failure();
        public Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken) => throw Failure();
    }
}
