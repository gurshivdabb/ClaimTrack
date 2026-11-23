using ClaimTrack.Api.Enums;

namespace ClaimTrack.Api.Dtos.Claims;

// One step in a claim's history. Shows who acted by name and role, but not their email.
public record AuditLogEntryResponse(
    Guid Id,
    AuditAction Action,
    ClaimStatus? FromStatus,
    ClaimStatus? ToStatus,
    string? Details,
    DateTimeOffset Timestamp,
    Guid ActorUserId,
    string ActorName,
    Role ActorRole);
