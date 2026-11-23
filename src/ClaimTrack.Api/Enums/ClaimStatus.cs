namespace ClaimTrack.Api.Enums;

// Allowed transitions between these values are listed in requirements §4 (BR-05).
public enum ClaimStatus
{
    Submitted,
    UnderReview,
    Approved,
    Denied,
    Withdrawn
}
