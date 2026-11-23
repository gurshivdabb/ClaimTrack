using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace ClaimTrack.IntegrationTests.Infrastructure;

// The in-memory test server has no real network, so every request has the same (empty) client IP.
// This test-only filter runs first in the pipeline and sets the IP from a header, so rate-limiting
// tests can act as different clients. It's only ever added to test copies of the API.
public sealed class TestClientIpStartupFilter : IStartupFilter
{
    public const string HeaderName = "X-Test-Client-Ip";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            if (IPAddress.TryParse(context.Request.Headers[HeaderName], out var ip))
            {
                context.Connection.RemoteIpAddress = ip;
            }
            await nextMiddleware();
        });
        next(app);
    };
}
