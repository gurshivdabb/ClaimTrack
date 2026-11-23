using System.Security.Claims;
using ClaimTrack.Api.Common;
using Serilog;
using Serilog.Events;

namespace ClaimTrack.Api.Middleware;

// Adds who made the request to Serilog's one-line request summary, so a log search can answer
// "every failed request for this user". Only the user's ID and role: no email, no name, and
// never the token itself.
public static class RequestLogEnricher
{
    public static void Enrich(IDiagnosticContext diagnosticContext, HttpContext httpContext)
    {
        var user = httpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return;
        }

        diagnosticContext.Set("UserId", user.FindFirstValue(JwtOptions.UserIdClaim));
        diagnosticContext.Set("UserRole", user.FindFirstValue(JwtOptions.RoleClaim));
    }

    // Level of the request summary line. Errors and 5xx are Error, as before. Successful health
    // probes arrive every few seconds from load balancers, so they're Verbose (hidden by default);
    // a failing probe is still a 503 and still logged as an Error.
    public static LogEventLevel GetLevel(HttpContext httpContext, double elapsedMilliseconds, Exception? exception)
    {
        if (exception is not null || httpContext.Response.StatusCode >= 500)
        {
            return LogEventLevel.Error;
        }

        return httpContext.Request.Path.StartsWithSegments(HealthEndpoints.Ready)
            ? LogEventLevel.Verbose
            : LogEventLevel.Information;
    }
}
