using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ClaimTrack.Api.Common;

// Settings from the "Jwt" configuration section. Issuer and Audience live in appsettings.json;
// SigningKey is a secret, so it comes from user-secrets locally and an environment variable in Docker.
public class JwtOptions
{
    public const string SectionName = "Jwt";

    // HS256 needs a key of at least 256 bits (32 bytes).
    public const int MinimumSigningKeyBytes = 32;

    // Claim names inside the token. "sub" (subject) is the standard name for the user's ID.
    public const string UserIdClaim = JwtRegisteredClaimNames.Sub;
    public const string EmailClaim = JwtRegisteredClaimNames.Email;
    public const string RoleClaim = "role";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;

    public bool IsValid =>
        Issuer.Length > 0 &&
        Audience.Length > 0 &&
        Encoding.UTF8.GetByteCount(SigningKey) >= MinimumSigningKeyBytes;

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));

    // The one set of rules for checking a token. Program.cs and the tests both use it,
    // so what the tests prove is exactly what the API does.
    public TokenValidationParameters CreateValidationParameters(TimeProvider timeProvider) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = CreateSigningKey(),

        // Only accept the algorithm we sign with, so a token can't pick a weaker one.
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

        // Requirements §7: tokens expire after 60 minutes. Our own check replaces the library's,
        // for two reasons: it reads the injectable TimeProvider (so tests can control time),
        // and it allows no grace period (the library adds 5 minutes by default).
        ValidateLifetime = true,
        RequireExpirationTime = true,
        LifetimeValidator = (notBefore, expires, _, _) => IsWithinLifetime(notBefore, expires, timeProvider),

        // Lets ASP.NET Core find the user's ID and role under our short claim names.
        NameClaimType = UserIdClaim,
        RoleClaimType = RoleClaim
    };

    private static bool IsWithinLifetime(DateTime? notBefore, DateTime? expires, TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return expires is not null
            && now < expires
            && (notBefore is null || now >= notBefore);
    }
}
