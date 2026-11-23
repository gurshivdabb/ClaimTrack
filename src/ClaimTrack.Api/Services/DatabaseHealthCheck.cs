using ClaimTrack.Api.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ClaimTrack.Api.Services;

// Part of GET /health: can the API reach SQL Server? If not, it can't do real work, so a load
// balancer should stop sending it traffic. The reason goes to the logs; the HTTP response only
// ever says "Unhealthy", so nothing about the database is revealed to callers.
public class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // An unreachable SQL Server took ~15 s to report, far too slow for a load balancer, and the
        // check's 3-second timeout was ignored: SqlClient can block the calling thread (e.g. resolving
        // a host that's gone) before it even returns a Task, and doesn't always stop when cancelled.
        // Task.Run moves that blocking work off this thread, and WaitAsync stops waiting when the
        // timeout fires; an abandoned attempt finishes in the background and is discarded.
        var canConnect = await Task.Run(() => db.Database.CanConnectAsync(cancellationToken), cancellationToken)
            .WaitAsync(cancellationToken);

        return canConnect
            ? HealthCheckResult.Healthy()
            : new HealthCheckResult(context.Registration.FailureStatus, "Cannot connect to the database.");
    }
}
