using ClaimTrack.Api.Enums;

namespace ClaimTrack.Api.Entities;

public class Claim
{
    public Guid Id { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;

    public Guid PolicyId { get; set; }
    public Policy Policy { get; set; } = null!;

    public DateOnly IncidentDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal ClaimedAmount { get; set; }
    public ClaimStatus Status { get; set; }

    // The Adjuster reviewing this claim; null until a supervisor assigns one.
    public Guid? AssignedAdjusterId { get; set; }
    public User? AssignedAdjuster { get; set; }

    // Workflow fields, filled in by the Phase 2 workflow endpoints.
    public Recommendation? Recommendation { get; set; }
    public string? AdjusterNotes { get; set; }
    public string? DecisionReason { get; set; }

    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }

    // Set once by OverdueClaimsJob when the claim has been UnderReview for too long (14 days by
    // default). Kept after a decision, as a record that the claim went overdue.
    public DateTimeOffset? FlaggedOverdueAt { get; set; }
}
