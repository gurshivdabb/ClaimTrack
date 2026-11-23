using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ClaimTrack.Api.Common;

// All rate limiting in one place (built into ASP.NET Core, no package):
// - "auth" policy on login and register, counted per client IP;
// - a general limit on everything else, counted per logged-in user (or IP when anonymous);
// - health checks never limited, because load balancers probe them constantly.
// A blocked request gets 429 Too Many Requests with a Retry-After header, immediately (no queue).
public static class RateLimitingSetup
{
    public static IServiceCollection AddClaimTrackRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitingOptions>()
            .BindConfiguration(RateLimitingOptions.SectionName)
            .Validate(options => options.IsValid, "RateLimiting limits and windows must be greater than 0.")
            .ValidateOnStart();

        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingOptions>>((limiter, settings) =>
            {
                var auth = settings.Value.Auth;
                var general = settings.Value.General;

                limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                limiter.AddPolicy(RateLimitingOptions.AuthPolicy, httpContext =>
                    RateLimitPartition.GetSlidingWindowLimiter(ClientIp(httpContext), _ => SlidingWindow(auth)));

                limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                    httpContext.Request.Path.StartsWithSegments(HealthEndpoints.Ready)
                        ? RateLimitPartition.GetNoLimiter("health")
                        : RateLimitPartition.GetSlidingWindowLimiter(UserOrIp(httpContext), _ => SlidingWindow(general)));

                limiter.OnRejected = OnRejectedAsync;
            });

        return services;
    }

    private const int SegmentsPerWindow = 6;

    // How long the client should wait. Some limiters report it; the sliding-window one doesn't, so
    // it's worked out: used permits come back one segment at a time (window ÷ 6), so that's the
    // earliest a retry can succeed. The window is the auth one if this endpoint uses the auth policy.
    private static int RetryAfterSeconds(OnRejectedContext context, HttpContext httpContext)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var reported))
        {
            return (int)Math.Ceiling(reported.TotalSeconds);
        }

        var settings = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
        var policy = httpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
        var window = policy == RateLimitingOptions.AuthPolicy ? settings.Auth : settings.General;

        return (int)Math.Ceiling((double)window.WindowSeconds / SegmentsPerWindow);
    }

    // A sliding window counts the last N seconds, split into 6 segments. With a fixed per-minute
    // window, a client could send the full limit at 12:00:59 and again at 12:01:00.
    // QueueLimit 0: rejected straight away, so an attacker can't tie up the server waiting.
    private static SlidingWindowRateLimiterOptions SlidingWindow(RateLimitSettings settings) => new()
    {
        PermitLimit = settings.PermitLimit,
        Window = TimeSpan.FromSeconds(settings.WindowSeconds),
        SegmentsPerWindow = SegmentsPerWindow,
        QueueLimit = 0
    };

    // The connection's IP. Behind a proxy or load balancer this would be the proxy's address for
    // everyone; production would configure ForwardedHeaders with the known proxies only (trusting
    // X-Forwarded-For from anyone would let attackers fake a new IP per request).
    private static string ClientIp(HttpContext httpContext) =>
        "ip:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

    private static string UserOrIp(HttpContext httpContext)
    {
        var userId = httpContext.User.FindFirstValue(JwtOptions.UserIdClaim);
        return userId is null ? ClientIp(httpContext) : "user:" + userId;
    }

    private static async ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;

        httpContext.Response.Headers.RetryAfter =
            RetryAfterSeconds(context, httpContext).ToString(CultureInfo.InvariantCulture);

        // The path, not the IP: consistent with keeping personal data out of the logs.
        httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("ClaimTrack.RateLimiting")
            .LogWarning("Rate limit exceeded for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        await httpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                // Same shape as the API's other errors, which link to the RFC defining their status.
                Type = "https://tools.ietf.org/html/rfc6585#section-4",
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests.",
                Detail = "Too many requests. Wait and try again."
            }
        });
    }
}
