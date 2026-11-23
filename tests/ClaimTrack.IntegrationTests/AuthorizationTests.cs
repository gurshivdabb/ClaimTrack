using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AwesomeAssertions;
using ClaimTrack.Api.Common;
using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Enums;
using ClaimTrack.Api.Services;
using ClaimTrack.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace ClaimTrack.IntegrationTests;

// 401 = no valid token ("who are you?"). 403 = valid token, wrong role ("you can't do this").
[Collection(ApiCollection.Name)]
public class AuthorizationTests(ClaimTrackApiFactory factory)
{
    private const string SomeId = "00000000-0000-0000-0000-000000000001";

    private static HttpRequestMessage Request(string method, string url) => new(new HttpMethod(method), url)
    {
        // The body doesn't matter: authorization is checked before the body is read.
        Content = new StringContent("{}", Encoding.UTF8, "application/json")
    };

    [Theory]
    [InlineData("/api/policies")]
    [InlineData("/api/claims")]
    public async Task ProtectedEndpoint_NoToken_Returns401ProblemDetails(string url)
    {
        var response = await factory.CreateClient().GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task ProtectedEndpoint_ExpiredToken_Returns401()
    {
        // A token issued 61 minutes before "now", so it expired a minute ago (requirements §7).
        var token = CreateTokenWith(
            new FakeTimeProvider(ClaimTrackApiFactory.Now.AddMinutes(-61)), ClaimTrackApiFactory.TestSigningKey);

        var response = await SendWithTokenAsync(token);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_TokenSignedWithAnotherKey_Returns401()
    {
        // A forged token: right format and claims, but not signed with the API's key.
        var token = CreateTokenWith(
            new FakeTimeProvider(ClaimTrackApiFactory.Now), "an-attackers-key-that-is-at-least-32-bytes");

        var response = await SendWithTokenAsync(token);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_GarbageToken_Returns401()
    {
        var response = await SendWithTokenAsync("not-a-jwt");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("POST", "/api/policies", Role.Adjuster)]
    [InlineData("POST", "/api/policies", Role.Policyholder)]
    [InlineData("PUT", $"/api/policies/{SomeId}", Role.Policyholder)]
    [InlineData("DELETE", $"/api/policies/{SomeId}", Role.Adjuster)]
    [InlineData("POST", "/api/claims", Role.Supervisor)]
    [InlineData("POST", "/api/claims", Role.Adjuster)]
    [InlineData("PUT", $"/api/claims/{SomeId}", Role.Supervisor)]
    [InlineData("POST", $"/api/claims/{SomeId}/assign", Role.Adjuster)]
    [InlineData("POST", $"/api/claims/{SomeId}/assign", Role.Policyholder)]
    [InlineData("POST", $"/api/claims/{SomeId}/start-review", Role.Supervisor)]
    [InlineData("POST", $"/api/claims/{SomeId}/start-review", Role.Policyholder)]
    [InlineData("POST", $"/api/claims/{SomeId}/recommendation", Role.Supervisor)]
    [InlineData("POST", $"/api/claims/{SomeId}/recommendation", Role.Policyholder)]
    [InlineData("POST", $"/api/claims/{SomeId}/decision", Role.Adjuster)]
    [InlineData("POST", $"/api/claims/{SomeId}/decision", Role.Policyholder)]
    [InlineData("POST", $"/api/claims/{SomeId}/withdraw", Role.Adjuster)]
    [InlineData("POST", $"/api/claims/{SomeId}/withdraw", Role.Supervisor)]
    public async Task RoleRestrictedEndpoint_WrongRole_Returns403ProblemDetails(string method, string url, Role role)
    {
        var client = await factory.CreateClientForNewUserAsync(role);

        var response = await client.SendAsync(Request(method, url));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Theory]
    [InlineData(Role.Policyholder)]
    [InlineData(Role.Adjuster)]
    [InlineData(Role.Supervisor)]
    public async Task ReadEndpoints_AnyLoggedInRole_Returns200(Role role)
    {
        var client = await factory.CreateClientForNewUserAsync(role);

        (await client.GetAsync("/api/policies")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/claims")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<HttpResponseMessage> SendWithTokenAsync(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync("/api/policies");
    }

    // Uses the real TokenService with a chosen clock and key, to build tokens the API must reject.
    private string CreateTokenWith(TimeProvider clock, string signingKey)
    {
        var apiSettings = factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var settings = new JwtOptions { Issuer = apiSettings.Issuer, Audience = apiSettings.Audience, SigningKey = signingKey };
        var user = new User { Id = ClaimTrackApiFactory.SupervisorId, Email = "s@test.local", Role = Role.Supervisor };

        return new TokenService(Options.Create(settings), clock).CreateToken(user).AccessToken;
    }
}
