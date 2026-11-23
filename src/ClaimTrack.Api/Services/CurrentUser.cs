using System.Security.Claims;
using ClaimTrack.Api.Common;
using ClaimTrack.Api.Enums;

namespace ClaimTrack.Api.Services;

// Reads the user from the validated JWT. The JWT middleware has already checked the
// token's signature and expiry, so the claims here can be trusted.
public class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid Id => Guid.TryParse(GetClaim(JwtOptions.UserIdClaim), out var id)
        ? id
        : throw new InvalidOperationException("The token's user ID claim is not a valid GUID.");

    public Role Role => Enum.TryParse<Role>(GetClaim(JwtOptions.RoleClaim), out var role) && Enum.IsDefined(role)
        ? role
        : throw new InvalidOperationException("The token's role claim is not a known role.");

    private string GetClaim(string claimType)
    {
        var user = httpContextAccessor.HttpContext?.User;

        // Reaching here without a logged-in user means an endpoint is missing [Authorize]: a bug, not user error.
        if (user?.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException("No authenticated user. Is the endpoint missing [Authorize]?");
        }

        return user.FindFirstValue(claimType)
            ?? throw new InvalidOperationException($"The token has no '{claimType}' claim.");
    }
}
