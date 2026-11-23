using System.Globalization;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Entities;

namespace ClaimTrack.Api.Services;

// The halves of BR-01 and BR-02 that need the policy's data. Input-only checks are in
// ClaimDetailsValidator. Kept separate from ClaimService so the rules can be unit tested
// without a database.
public static class ClaimPolicyRules
{
    // Returns null when the claim fits the policy, otherwise the reason it doesn't.
    public static string? Check(IClaimDetails claim, Policy policy)
    {
        // Messages use the invariant culture so they read the same on every server,
        // and money always shows two decimals (5000.00) however the value was created.

        // BR-01: no more than the coverage limit.
        if (claim.ClaimedAmount > policy.CoverageLimit)
        {
            return string.Create(CultureInfo.InvariantCulture,
                $"Claimed amount exceeds the policy's coverage limit of {policy.CoverageLimit:0.00}.");
        }

        // BR-02: the incident must happen while the policy is active.
        if (claim.IncidentDate < policy.StartDate || claim.IncidentDate > policy.EndDate)
        {
            return string.Create(CultureInfo.InvariantCulture,
                $"Incident date must be between {policy.StartDate:yyyy-MM-dd} and {policy.EndDate:yyyy-MM-dd}.");
        }

        return null;
    }
}
