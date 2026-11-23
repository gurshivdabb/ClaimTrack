using ClaimTrack.Api.Common;
using ClaimTrack.Api.Data;
using ClaimTrack.Api.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClaimTrack.Api.Services;

// The rule behind the overdue-claims job, as an ordinary scoped service so it can be tested
// directly. OverdueClaimsJob only decides *when* it runs.
public class OverdueClaimsService(
    AppDbContext db, IOptions<OverdueClaimsOptions> options, TimeProvider timeProvider) : IOverdueClaimsService
{
    public async Task<int> FlagOverdueClaimsAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var cutoff = now.AddDays(-options.Value.ThresholdDays);

        // One SQL UPDATE. The clock starts when the review started (its ReviewStarted audit entry,
        // the single source of truth), and a recommendation doesn't reset it: a claim waiting for
        // the supervisor is just as stuck for the customer. "FlaggedOverdueAt == null" makes it
        // idempotent: running twice, or on two API copies at once, never re-flags a claim.
        return await db.Claims
            .Where(c => c.Status == ClaimStatus.UnderReview && c.FlaggedOverdueAt == null)
            .Where(c => db.AuditLog
                .Where(a => a.ClaimId == c.Id && a.Action == AuditAction.ReviewStarted)
                .Max(a => (DateTimeOffset?)a.Timestamp) <= cutoff)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.FlaggedOverdueAt, now), cancellationToken);
    }
}
