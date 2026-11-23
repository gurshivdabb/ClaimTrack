namespace ClaimTrack.Api.Enums;

// What happened to a claim (requirements §3). Stored as text, like every enum.
public enum AuditAction
{
    Submitted,
    Updated,
    Assigned,
    ReviewStarted,
    Recommended,
    Approved,
    Denied,
    Withdrawn,
    DocumentUploaded
}
