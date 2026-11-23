namespace ClaimTrack.Api.Dtos.Claims;

// The fields shared by submit and update requests, so their rules are written once
// in ClaimDetailsValidator and ClaimService.
public interface IClaimDetails
{
    DateOnly IncidentDate { get; }
    string Description { get; }
    decimal ClaimedAmount { get; }
}
