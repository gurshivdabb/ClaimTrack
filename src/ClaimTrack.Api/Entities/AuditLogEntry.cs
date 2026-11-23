using ClaimTrack.Api.Enums;

namespace ClaimTrack.Api.Entities;

// One thing that happened to a claim: who did what, when, and how the status changed.
// BR-08: append-only. AppDbContext refuses to save a change to or removal of an entry.
public class AuditLogEntry
{
    public Guid Id { get; set; }

    public Guid ClaimId { get; set; }
    public Claim Claim { get; set; } = null!;

    // The user who performed the action.
    public Guid ActorUserId { get; set; }
    public User Actor { get; set; } = null!;

    public AuditAction Action { get; set; }

    // Null when the action doesn't change the status (assignment, recommendation, edit),
    // and FromStatus is null for the first entry (submission).
    public ClaimStatus? FromStatus { get; set; }
    public ClaimStatus? ToStatus { get; set; }

    // e.g. "Assigned to Jane Doe", or the reason for a denial.
    public string? Details { get; set; }

    public DateTimeOffset Timestamp { get; set; }
}
