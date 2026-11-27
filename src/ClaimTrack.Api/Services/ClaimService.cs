using ClaimTrack.Api.Common;
using ClaimTrack.Api.Data;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClaimTrack.Api.Services;

public class ClaimService(
    AppDbContext db,
    INumberGenerator numberGenerator,
    TimeProvider timeProvider,
    ICurrentUser currentUser,
    IAuditService audit)
    : IClaimService
{
    public async Task<PagedResult<ClaimResponse>> GetAllAsync(ClaimListQuery query, CancellationToken cancellationToken)
    {
        // BR-09 first, so the total only counts claims this user is allowed to see.
        var claims = db.Claims.VisibleTo(currentUser).AsNoTracking();

        if (query.Status is not null)
        {
            claims = claims.Where(c => c.Status == query.Status);
        }

        if (query.Overdue is not null)
        {
            claims = query.Overdue.Value
                ? claims.Where(c => c.FlaggedOverdueAt != null)
                : claims.Where(c => c.FlaggedOverdueAt == null);
        }

        // Two queries: COUNT(*) for the total, then just one page of rows.
        var totalCount = await claims.CountAsync(cancellationToken);
        var items = await claims
            .ApplySort(query.Sort)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(c => c.ToResponse())
            .ToListAsync(cancellationToken);

        return new PagedResult<ClaimResponse>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<Result<ClaimResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var claim = await db.Claims.VisibleTo(currentUser) // BR-09
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return claim is null
            ? Result<ClaimResponse>.NotFound()
            : Result<ClaimResponse>.Ok(claim.ToResponse());
    }

    public async Task<Result<ClaimResponse>> SubmitAsync(
        SubmitClaimRequest request, CancellationToken cancellationToken)
    {
        // BR-03: only against a policy you hold. Only policyholders can submit, and the visibility
        // filter shows a policyholder only their own policies, so someone else's policy gets the
        // same answer as a missing one and the API doesn't reveal that it exists (BR-09).
        var policy = await db.Policies.VisibleTo(currentUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PolicyId, cancellationToken);
        if (policy is null)
        {
            return Result<ClaimResponse>.Invalid("Policy not found.");
        }

        var policyError = ClaimPolicyRules.Check(request, policy);
        if (policyError is not null)
        {
            return Result<ClaimResponse>.Invalid(policyError);
        }

        var claim = new Claim
        {
            ClaimNumber = await numberGenerator.NextClaimNumberAsync(cancellationToken),
            PolicyId = request.PolicyId,
            IncidentDate = request.IncidentDate,
            Description = request.Description,
            ClaimedAmount = request.ClaimedAmount,
            Status = ClaimStatus.Submitted,
            SubmittedAt = timeProvider.GetUtcNow()
        };

        db.Claims.Add(claim);
        audit.Record(claim, AuditAction.Submitted, toStatus: ClaimStatus.Submitted); // BR-07
        await db.SaveChangesAsync(cancellationToken);

        return Result<ClaimResponse>.Ok(claim.ToResponse());
    }

    public async Task<Result<ClaimResponse>> UpdateAsync(
        Guid id, UpdateClaimRequest request, CancellationToken cancellationToken)
    {
        var claim = await FindVisibleClaimAsync(id, cancellationToken);
        if (claim is null)
        {
            return Result<ClaimResponse>.NotFound();
        }

        // Requirements §6: a claim can only be edited while it is still Submitted.
        if (claim.Status != ClaimStatus.Submitted)
        {
            return Result<ClaimResponse>.Conflict(
                $"Only submitted claims can be updated. This claim is {claim.Status}.");
        }

        var policy = await db.Policies
            .AsNoTracking()
            .FirstAsync(p => p.Id == claim.PolicyId, cancellationToken);

        var policyError = ClaimPolicyRules.Check(request, policy);
        if (policyError is not null)
        {
            return Result<ClaimResponse>.Invalid(policyError);
        }

        // BR-07: describe the edit before overwriting the old values.
        audit.Record(claim, AuditAction.Updated, details: ClaimEditDetails.Describe(claim, request));

        claim.IncidentDate = request.IncidentDate;
        claim.Description = request.Description;
        claim.ClaimedAmount = request.ClaimedAmount;

        return await SaveAsync(claim, cancellationToken);
    }

    public async Task<Result<ClaimResponse>> AssignAsync(
        Guid id, AssignClaimRequest request, CancellationToken cancellationToken)
    {
        var claim = await FindVisibleClaimAsync(id, cancellationToken);
        if (claim is null)
        {
            return Result<ClaimResponse>.NotFound();
        }

        if (!ClaimLifecycle.CanBeAssigned(claim.Status))
        {
            return Result<ClaimResponse>.Conflict(
                $"Claims can only be assigned while Submitted or UnderReview. This claim is {claim.Status}.");
        }

        // BR-04: the assignee must have the Adjuster role. Their name is for the audit entry.
        var adjusterName = await db.Users
            .Where(u => u.Id == request.AdjusterId && u.Role == Role.Adjuster)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken);
        if (adjusterName is null)
        {
            return Result<ClaimResponse>.Invalid("Assignee must be an existing adjuster.");
        }

        // §4: assignment doesn't change the status. Reassigning simply replaces the adjuster.
        claim.AssignedAdjusterId = request.AdjusterId;

        audit.Record(claim, AuditAction.Assigned, details: $"Assigned to {adjusterName}"); // BR-07
        return await SaveAsync(claim, cancellationToken);
    }

    // Submitted → UnderReview. Only the assigned adjuster can see the claim (BR-09),
    // so only they can get past the 404.
    public Task<Result<ClaimResponse>> StartReviewAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, ClaimStatus.UnderReview, AuditAction.ReviewStarted, cancellationToken);

    // Submitted → Withdrawn. Only the owning policyholder can see the claim (BR-09).
    public Task<Result<ClaimResponse>> WithdrawAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, ClaimStatus.Withdrawn, AuditAction.Withdrawn, cancellationToken);

    // UnderReview → Approved or Denied, by a supervisor.
    public Task<Result<ClaimResponse>> DecideAsync(
        Guid id, DecideClaimRequest request, CancellationToken cancellationToken)
    {
        var approve = request.Decision == ClaimDecision.Approve;
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        return ChangeStatusAsync(
            id,
            approve ? ClaimStatus.Approved : ClaimStatus.Denied,
            approve ? AuditAction.Approved : AuditAction.Denied,
            cancellationToken,
            auditDetails: reason,
            // §4: the supervisor decides only after the adjuster has recommended.
            // The decision may differ from the recommendation: it's advice, not a vote.
            extraRule: claim => claim.Recommendation is null
                ? "A recommendation is required before a decision."
                : null,
            onChange: claim =>
            {
                // BR-06 is checked by DecideClaimRequestValidator; the reason is optional when approving.
                claim.DecisionReason = reason;
                claim.DecidedAt = timeProvider.GetUtcNow();
            });
    }

    // Not a status change: the claim stays UnderReview. A newer recommendation replaces the old one
    // until the supervisor decides.
    public async Task<Result<ClaimResponse>> RecommendAsync(
        Guid id, RecommendClaimRequest request, CancellationToken cancellationToken)
    {
        var claim = await FindVisibleClaimAsync(id, cancellationToken);
        if (claim is null)
        {
            return Result<ClaimResponse>.NotFound();
        }

        if (claim.Status != ClaimStatus.UnderReview)
        {
            return Result<ClaimResponse>.Conflict(
                $"Recommendations can only be made while a claim is UnderReview. This claim is {claim.Status}.");
        }

        claim.Recommendation = request.Recommendation;
        claim.AdjusterNotes = request.Notes.Trim();

        audit.Record(claim, AuditAction.Recommended, details: $"Recommended {request.Recommendation}"); // BR-07
        return await SaveAsync(claim, cancellationToken);
    }

    // BR-09: every lookup by ID goes through the visibility filter, so a claim you can't see is 404.
    private Task<Claim?> FindVisibleClaimAsync(Guid id, CancellationToken cancellationToken) =>
        db.Claims.VisibleTo(currentUser).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    // The one place a claim's status changes: load it, check BR-05, check any extra rule,
    // apply the change, record it in the audit log, save. Every status change goes through here.
    private async Task<Result<ClaimResponse>> ChangeStatusAsync(
        Guid id,
        ClaimStatus newStatus,
        AuditAction auditAction,
        CancellationToken cancellationToken,
        string? auditDetails = null,
        Func<Claim, string?>? extraRule = null,
        Action<Claim>? onChange = null)
    {
        var claim = await FindVisibleClaimAsync(id, cancellationToken);
        if (claim is null)
        {
            return Result<ClaimResponse>.NotFound();
        }

        // BR-05: only the transitions in requirements §4 are allowed.
        if (!ClaimLifecycle.CanTransition(claim.Status, newStatus))
        {
            return Result<ClaimResponse>.Conflict(ClaimLifecycle.TransitionError(claim.Status, newStatus));
        }

        var ruleError = extraRule?.Invoke(claim);
        if (ruleError is not null)
        {
            return Result<ClaimResponse>.Conflict(ruleError);
        }

        var oldStatus = claim.Status;
        claim.Status = newStatus;
        onChange?.Invoke(claim);

        // BR-07: saved in the same SaveChangesAsync (one transaction) as the status change.
        audit.Record(claim, auditAction, oldStatus, newStatus, auditDetails);
        return await SaveAsync(claim, cancellationToken);
    }

    // Saves a change to an existing claim (and its audit entry) in one transaction.
    //
    // Optimistic concurrency: if someone else changed this claim after we loaded it, its RowVersion
    // no longer matches, EF Core saves nothing (not even the audit entry) and throws. That's an
    // expected outcome when two people act at once, not a bug, so it becomes a 409 like any other
    // conflict (BR-05: two decisions on one claim must never both succeed).
    private async Task<Result<ClaimResponse>> SaveAsync(Claim claim, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ClaimResponse>.Conflict(
                "This claim was changed by someone else at the same time. Reload it and try again.");
        }

        return Result<ClaimResponse>.Ok(claim.ToResponse());
    }
}
