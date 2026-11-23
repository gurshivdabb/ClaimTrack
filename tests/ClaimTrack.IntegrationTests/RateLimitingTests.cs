using System.Net;
using AwesomeAssertions;
using ClaimTrack.Api.Dtos.Auth;
using ClaimTrack.Api.Enums;
using ClaimTrack.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ClaimTrack.IntegrationTests;

// Each test uses its own copy of the API with tiny limits (3 per minute), so it starts with fresh
// counters and doesn't affect the other tests, which run with very high limits.
[Collection(ApiCollection.Name)]
public class RateLimitingTests(ClaimTrackApiFactory factory)
{
    private const int Limit = 3;

    private WebApplicationFactory<Program> LimitedApi(int authLimit = Limit, int generalLimit = Limit) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:Auth:PermitLimit"] = authLimit.ToString(),
                    ["RateLimiting:General:PermitLimit"] = generalLimit.ToString()
                }));
            builder.ConfigureTestServices(services => services.AddTransient<IStartupFilter, TestClientIpStartupFilter>());
        });

    private static HttpClient ClientFrom(WebApplicationFactory<Program> api, string ip)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add(TestClientIpStartupFilter.HeaderName, ip);
        return client;
    }

    private static Task<HttpResponseMessage> TryLoginAsync(HttpClient client) =>
        client.PostJsonAsync("/api/auth/login", new LoginRequest($"{Guid.NewGuid():N}@example.com", "Wrong-Password-1"));

    [Fact]
    public async Task Login_OverTheLimit_Returns429WithRetryAfterAsProblemDetails()
    {
        using var api = LimitedApi();
        var client = ClientFrom(api, "203.0.113.10");

        for (var attempt = 1; attempt <= Limit; attempt++)
        {
            (await TryLoginAsync(client)).StatusCode.Should().Be(HttpStatusCode.Unauthorized); // wrong password, but allowed
        }
        var blocked = await TryLoginAsync(client);

        blocked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        // A 60-second window in 6 segments: a slot frees up after at most 10 seconds.
        blocked.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(10), "the client should be told how long to wait");
        blocked.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await blocked.ReadAsync<ProblemDetails>();
        problem.Title.Should().Be("Too many requests.");
        problem.Type.Should().Be("https://tools.ietf.org/html/rfc6585#section-4");
    }

    [Fact]
    public async Task Login_DifferentIps_HaveSeparateLimits()
    {
        using var api = LimitedApi();
        var attacker = ClientFrom(api, "203.0.113.20");
        var someoneElse = ClientFrom(api, "198.51.100.7");

        for (var attempt = 0; attempt <= Limit; attempt++)
        {
            await TryLoginAsync(attacker);
        }

        (await TryLoginAsync(attacker)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await TryLoginAsync(someoneElse)).StatusCode.Should().Be(HttpStatusCode.Unauthorized); // not blocked
    }

    [Fact]
    public async Task GeneralLimit_OneUserOverTheLimit_DoesNotBlockAnotherUser()
    {
        using var api = LimitedApi(generalLimit: Limit);
        // Same IP for both: the general limit counts per logged-in user, not per IP.
        var userA = ClientFrom(api, "192.0.2.1");
        userA.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateToken(Guid.NewGuid(), Role.Supervisor));
        var userB = ClientFrom(api, "192.0.2.1");
        userB.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateToken(Guid.NewGuid(), Role.Supervisor));

        for (var call = 1; call <= Limit; call++)
        {
            (await userA.GetAsync("/api/policies")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await userA.GetAsync("/api/policies")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await userB.GetAsync("/api/policies")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthChecks_AreNeverLimited()
    {
        using var api = LimitedApi(generalLimit: 1);
        var loadBalancer = ClientFrom(api, "10.0.0.1");

        for (var probe = 0; probe < 20; probe++)
        {
            (await loadBalancer.GetAsync(probe % 2 == 0 ? "/health" : "/health/live"))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public void Startup_WithZeroLimit_FailsImmediately()
    {
        using var api = LimitedApi(authLimit: 0);

        var start = () => api.CreateClient();

        start.Should().Throw<OptionsValidationException>().WithMessage("*RateLimiting*");
    }
}
