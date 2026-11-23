using System.Security.Claims;
using AwesomeAssertions;
using ClaimTrack.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Events;

namespace ClaimTrack.UnitTests.Middleware;

public class RequestLogEnricherTests
{
    private sealed class RecordingDiagnosticContext : IDiagnosticContext
    {
        public Dictionary<string, object?> Properties { get; } = [];
        public void Set(string propertyName, object? value, bool destructureObjects = false) => Properties[propertyName] = value;
        public void SetException(Exception exception) { }
    }

    private static HttpContext RequestBy(params Claim[] claims) => new DefaultHttpContext
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(claims, claims.Length == 0 ? null : "Bearer"))
    };

    [Fact]
    public void Enrich_LoggedInUser_AddsOnlyIdAndRole()
    {
        var userId = Guid.NewGuid().ToString();
        var context = new RecordingDiagnosticContext();

        RequestLogEnricher.Enrich(context, RequestBy(
            new Claim("sub", userId), new Claim("role", "Adjuster"), new Claim("email", "jane@x.test")));

        // The email is in the token, but deliberately not logged.
        context.Properties.Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["UserId"] = userId,
            ["UserRole"] = "Adjuster"
        });
    }

    [Theory]
    [InlineData("/health", 200, LogEventLevel.Verbose)]       // routine probe: hidden
    [InlineData("/health/live", 200, LogEventLevel.Verbose)]
    [InlineData("/health", 503, LogEventLevel.Error)]         // failing probe: still loud
    [InlineData("/api/claims", 200, LogEventLevel.Information)]
    [InlineData("/api/claims", 404, LogEventLevel.Information)]
    [InlineData("/api/claims", 500, LogEventLevel.Error)]
    [InlineData("/healthy-cats", 200, LogEventLevel.Information)] // only the /health path segment counts
    public void GetLevel_PathAndStatus_PicksTheRightLevel(string path, int status, LogEventLevel expected)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        httpContext.Response.StatusCode = status;

        RequestLogEnricher.GetLevel(httpContext, 1.0, exception: null).Should().Be(expected);
    }

    [Fact]
    public void GetLevel_Exception_IsError()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/health";

        RequestLogEnricher.GetLevel(httpContext, 1.0, new InvalidOperationException()).Should().Be(LogEventLevel.Error);
    }

    [Fact]
    public void Enrich_AnonymousRequest_AddsNothing()
    {
        var context = new RecordingDiagnosticContext();

        RequestLogEnricher.Enrich(context, RequestBy());

        context.Properties.Should().BeEmpty();
    }
}
