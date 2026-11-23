using ClaimTrack.Api.Enums;

namespace ClaimTrack.Api.Dtos.Claims;

public record ClaimResponse(
    Guid Id,
    string ClaimNumber,
    Guid PolicyId,
    DateOnly IncidentDate,
    string Description,
    decimal ClaimedAmount,
    ClaimStatus Status,
    Guid? AssignedAdjusterId,
    Recommendation? Recommendation,
    string? AdjusterNotes,
    string? DecisionReason,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? DecidedAt,
    DateTimeOffset? FlaggedOverdueAt);
