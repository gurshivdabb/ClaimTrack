using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Enums;

namespace ClaimTrack.Api.Services;

// BR-09: users only see data their role allows. Every service query for policies or claims
// starts from these filters, so the rule lives in one place and can't be forgotten in one method.
// They run inside SQL (a WHERE clause), so other users' rows are never even loaded.
// Anything filtered out behaves as if it doesn't exist: missing from lists, 404 by ID.
public static class VisibilityRules
{
    public static IQueryable<Policy> VisibleTo(this IQueryable<Policy> policies, ICurrentUser user)
    {
        var userId = user.Id;

        return user.Role switch
        {
            Role.Supervisor => policies,
            Role.Policyholder => policies.Where(p => p.HolderId == userId),

            // Need-to-know: only the policies behind claims assigned to them, so they can
            // check the coverage limit and deductible while reviewing.
            Role.Adjuster => policies.Where(p => p.Claims.Any(c => c.AssignedAdjusterId == userId)),

            _ => throw new InvalidOperationException($"No visibility rule for role {user.Role}.")
        };
    }

    public static IQueryable<Claim> VisibleTo(this IQueryable<Claim> claims, ICurrentUser user)
    {
        var userId = user.Id;

        return user.Role switch
        {
            Role.Supervisor => claims,
            Role.Policyholder => claims.Where(c => c.Policy.HolderId == userId),
            Role.Adjuster => claims.Where(c => c.AssignedAdjusterId == userId),
            _ => throw new InvalidOperationException($"No visibility rule for role {user.Role}.")
        };
    }
}
