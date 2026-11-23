using ClaimTrack.Api.Enums;

namespace ClaimTrack.Api.Dtos.Claims;

// Decision is nullable for the same reason as in RecommendClaimRequest: a missing field
// must be rejected, never treated as "Approve". Reason is required only when denying (BR-06).
public record DecideClaimRequest(ClaimDecision? Decision, string? Reason);
