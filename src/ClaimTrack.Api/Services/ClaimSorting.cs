using ClaimTrack.Api.Entities;

namespace ClaimTrack.Api.Services;

// The sort options for the claims list, in one place: the validator checks against the same
// field names that ApplySort understands. "-" in front of a field means descending.
public static class ClaimSorting
{
    public const string DefaultSort = "-submittedAt";

    public static readonly IReadOnlyList<string> Fields = ["submittedAt", "incidentDate", "claimedAmount", "claimNumber"];

    public static bool IsValid(string? sort) =>
        sort is null || Fields.Contains(FieldName(sort), StringComparer.OrdinalIgnoreCase);

    // Every sort ends with Id in the same direction. Without a unique tie-breaker, claims with
    // equal values (e.g. the same SubmittedAt) can come back in a different order on each query,
    // so a claim could appear on two pages or on none. Sequential GUIDs follow creation order.
    public static IQueryable<Claim> ApplySort(this IQueryable<Claim> claims, string? sort)
    {
        sort ??= DefaultSort;
        var descending = sort.StartsWith('-');

        return FieldName(sort).ToLowerInvariant() switch
        {
            "submittedat" => descending
                ? claims.OrderByDescending(c => c.SubmittedAt).ThenByDescending(c => c.Id)
                : claims.OrderBy(c => c.SubmittedAt).ThenBy(c => c.Id),
            "incidentdate" => descending
                ? claims.OrderByDescending(c => c.IncidentDate).ThenByDescending(c => c.Id)
                : claims.OrderBy(c => c.IncidentDate).ThenBy(c => c.Id),
            "claimedamount" => descending
                ? claims.OrderByDescending(c => c.ClaimedAmount).ThenByDescending(c => c.Id)
                : claims.OrderBy(c => c.ClaimedAmount).ThenBy(c => c.Id),
            "claimnumber" => descending
                ? claims.OrderByDescending(c => c.ClaimNumber).ThenByDescending(c => c.Id)
                : claims.OrderBy(c => c.ClaimNumber).ThenBy(c => c.Id),
            _ => throw new ArgumentException($"Unknown sort field '{sort}'.", nameof(sort)) // the validator stops this first
        };
    }

    // Removes exactly one leading "-", so "--submittedAt" stays invalid.
    private static string FieldName(string sort) => sort.StartsWith('-') ? sort[1..] : sort;
}
