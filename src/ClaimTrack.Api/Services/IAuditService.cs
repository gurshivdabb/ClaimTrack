using ClaimTrack.Api.Common;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Enums;

namespace ClaimTrack.Api.Services;

public interface IAuditService
{
    // Adds an entry without saving. The caller's SaveChangesAsync saves it together with
    // the change it describes, in one transaction (BR-07).
    void Record(Claim claim, AuditAction action, ClaimStatus? fromStatus = null, ClaimStatus? toStatus = null, string? details = null);

    // The claim's entries, oldest first. NotFound if the current user can't see the claim (BR-09).
    Task<Result<List<AuditLogEntryResponse>>> GetHistoryAsync(Guid claimId, CancellationToken cancellationToken);
}
