using System.Globalization;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Entities;

namespace ClaimTrack.Api.Services;

// BR-07: the audit Details for an edit, e.g. "ClaimedAmount 1000.00 → 4000.00; Description changed".
// Pure logic, so it's unit-tested on its own.
public static class ClaimEditDetails
{
    public static string Describe(Claim before, IClaimDetails after)
    {
        var changes = new List<string>();

        if (before.ClaimedAmount != after.ClaimedAmount)
        {
            changes.Add(string.Create(CultureInfo.InvariantCulture,
                $"ClaimedAmount {before.ClaimedAmount:0.00} → {after.ClaimedAmount:0.00}"));
        }

        if (before.IncidentDate != after.IncidentDate)
        {
            changes.Add(string.Create(CultureInfo.InvariantCulture,
                $"IncidentDate {before.IncidentDate:yyyy-MM-dd} → {after.IncidentDate:yyyy-MM-dd}"));
        }

        // The text itself can be up to 2000 characters, too long to copy in twice.
        if (before.Description != after.Description)
        {
            changes.Add("Description changed");
        }

        return changes.Count == 0 ? "No fields changed" : string.Join("; ", changes);
    }
}
