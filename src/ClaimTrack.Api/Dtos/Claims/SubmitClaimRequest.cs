namespace ClaimTrack.Api.Dtos.Claims;

public record SubmitClaimRequest(
    Guid PolicyId,
    DateOnly IncidentDate,
    string Description,
    decimal ClaimedAmount) : IClaimDetails;
