namespace ClaimTrack.Api.Common;

// One rate limit: at most PermitLimit requests in any WindowSeconds-long period.
public class RateLimitSettings
{
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }

    public bool IsValid => PermitLimit > 0 && WindowSeconds > 0;
}
