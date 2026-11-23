namespace ClaimTrack.Api.Enums;

// The supervisor's final decision. Kept separate from Recommendation (the adjuster's advice):
// same values, different concept and different person, so they can change independently.
public enum ClaimDecision
{
    Approve,
    Deny
}
