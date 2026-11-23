namespace ClaimTrack.Api.Common;

// The health check URLs (requirements §6), in one place for Program.cs and request logging.
public static class HealthEndpoints
{
    // "Can you do real work?" Includes the database. A load balancer stops sending traffic if not.
    public const string Ready = "/health";

    // "Is the process alive?" Nothing external. A platform restarts the app only if this fails.
    public const string Live = "/health/live";

    public const string ReadyTag = "ready";
}
