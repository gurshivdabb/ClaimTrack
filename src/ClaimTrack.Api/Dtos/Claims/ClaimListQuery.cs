using ClaimTrack.Api.Enums;

namespace ClaimTrack.Api.Dtos.Claims;

// Query string for GET /api/claims, e.g. ?status=UnderReview&overdue=true&page=1&pageSize=20&sort=-submittedAt.
// Every parameter is optional; the defaults give the first 20 claims, newest first.
// Overdue: true = only claims flagged as overdue, false = only claims that aren't, missing = both.
public record ClaimListQuery(
    ClaimStatus? Status = null,
    int Page = 1,
    int PageSize = ClaimListQuery.DefaultPageSize,
    string? Sort = null,
    bool? Overdue = null)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}
