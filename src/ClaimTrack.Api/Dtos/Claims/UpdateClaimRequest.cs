namespace ClaimTrack.Api.Dtos.Claims;

// The policy is not included: a claim can't be moved to a different policy.
public record UpdateClaimRequest(
    DateOnly IncidentDate,
    string Description,
    decimal ClaimedAmount) : IClaimDetails;
