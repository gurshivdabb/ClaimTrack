using ClaimTrack.Api.Entities;

namespace ClaimTrack.Api.Dtos.Claims;

public static class ClaimMappings
{
    public static ClaimResponse ToResponse(this Claim claim) => new(
        claim.Id,
        claim.ClaimNumber,
        claim.PolicyId,
        claim.IncidentDate,
        claim.Description,
        claim.ClaimedAmount,
        claim.Status,
        claim.AssignedAdjusterId,
        claim.Recommendation,
        claim.AdjusterNotes,
        claim.DecisionReason,
        claim.SubmittedAt,
        claim.DecidedAt,
        claim.FlaggedOverdueAt);
}
