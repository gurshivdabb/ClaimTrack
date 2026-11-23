namespace ClaimTrack.Api.Common;

// Settings from the "RateLimiting" section of appsettings.json. The defaults below apply if the
// section is missing; tests raise them so their hundreds of logins aren't blocked.
public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    // The policy name used by [EnableRateLimiting] on AuthController.
    public const string AuthPolicy = "auth";

    // Login and register, per client IP: slows down password guessing and account spam.
    public RateLimitSettings Auth { get; set; } = new() { PermitLimit = 5, WindowSeconds = 60 };

    // Every other endpoint, per logged-in user (or per IP when anonymous): stops one client
    // from hogging the API.
    public RateLimitSettings General { get; set; } = new() { PermitLimit = 100, WindowSeconds = 60 };

    public bool IsValid => Auth.IsValid && General.IsValid;
}
