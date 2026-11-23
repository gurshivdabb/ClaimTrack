using ClaimTrack.Api.Enums;

namespace ClaimTrack.Api.Dtos.Claims;

// Nullable so a missing field is a validation error, not a silent default of "Approve"
// (the first enum value is what a non-nullable enum gets when the JSON leaves it out).
public record RecommendClaimRequest(Recommendation? Recommendation, string Notes);
