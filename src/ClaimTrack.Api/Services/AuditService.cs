using ClaimTrack.Api.Common;
using ClaimTrack.Api.Data;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClaimTrack.Api.Services;

// Everything about the audit log: writing entries and reading a claim's history.
public class AuditService(AppDbContext db, ICurrentUser currentUser, TimeProvider timeProvider) : IAuditService
{
    // AppDbContext is scoped (one per request), so this service and ClaimService share the same
    // instance. Whatever Record adds is saved by ClaimService's single SaveChangesAsync, which
    // EF runs as one database transaction: the change and its audit entry are saved together, or neither is.
    public void Record(
        Claim claim, AuditAction action, ClaimStatus? fromStatus = null, ClaimStatus? toStatus = null, string? details = null)
    {
        db.AuditLog.Add(new AuditLogEntry
        {
            Claim = claim, // the navigation, so this also works for a claim that isn't saved yet
            ActorUserId = currentUser.Id,
            Action = action,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Details = details,
            Timestamp = timeProvider.GetUtcNow()
        });
    }

    public async Task<Result<List<AuditLogEntryResponse>>> GetHistoryAsync(
        Guid claimId, CancellationToken cancellationToken)
    {
        // BR-09: anyone who can see the claim can see its history; anyone else gets 404.
        var canSeeClaim = await db.Claims.VisibleTo(currentUser).AnyAsync(c => c.Id == claimId, cancellationToken);
        if (!canSeeClaim)
        {
            return Result<List<AuditLogEntryResponse>>.NotFound();
        }

        // Oldest first: a history is read as a story. Id breaks ties between entries with the
        // same timestamp, because EF gives SQL Server keys sequential GUIDs (creation order).
        var history = await db.AuditLog
            .Where(a => a.ClaimId == claimId)
            .OrderBy(a => a.Timestamp)
            .ThenBy(a => a.Id)
            .Select(AuditLogMappings.ToResponse)
            .ToListAsync(cancellationToken);

        return Result<List<AuditLogEntryResponse>>.Ok(history);
    }
}
