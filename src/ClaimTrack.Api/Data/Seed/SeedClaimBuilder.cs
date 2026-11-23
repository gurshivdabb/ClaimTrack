using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Enums;
using ClaimTrack.Api.Services;
using ClaimTrack.Api.Validators;

namespace ClaimTrack.Api.Data.Seed;

// Builds one demo claim and its audit trail step by step, the way it would have happened
// through the API, but with realistic past timestamps. Every step checks the same rules the
// API uses (BR-01, BR-02, BR-04, BR-05, BR-06), so seed data can never contain an impossible history.
// A broken rule throws: that's a bug in the seed data, caught the first time it runs.
public sealed class SeedClaimBuilder
{
    private readonly DateTimeOffset _now;
    private readonly List<AuditLogEntry> _auditLog = [];
    private DateTimeOffset _time;

    public Claim Claim { get; }
    public IReadOnlyList<AuditLogEntry> AuditLog => _auditLog;

    private SeedClaimBuilder(Claim claim, DateTimeOffset submittedAt, DateTimeOffset now)
    {
        Claim = claim;
        _time = submittedAt;
        _now = now;
    }

    public static SeedClaimBuilder Submit(
        Policy policy, string claimNumber, DateOnly incidentDate, string description, decimal amount,
        DateTimeOffset submittedAt, DateTimeOffset now)
    {
        var details = new SubmitClaimRequest(policy.Id, incidentDate, description, amount);
        EnsureFitsPolicy(details, policy, submittedAt);

        var claim = new Claim
        {
            ClaimNumber = claimNumber,
            Policy = policy,
            IncidentDate = incidentDate,
            Description = description,
            ClaimedAmount = amount,
            Status = ClaimStatus.Submitted,
            SubmittedAt = submittedAt
        };

        var builder = new SeedClaimBuilder(claim, submittedAt, now);
        builder.Record(AuditAction.Submitted, policy.HolderId, toStatus: ClaimStatus.Submitted);
        return builder;
    }

    public SeedClaimBuilder Edit(TimeSpan after, string description, decimal amount)
    {
        Advance(after);
        Ensure(Claim.Status == ClaimStatus.Submitted, "Only submitted claims can be edited.");

        var update = new UpdateClaimRequest(Claim.IncidentDate, description, amount);
        EnsureFitsPolicy(update, Claim.Policy, _time);
        Record(AuditAction.Updated, Claim.Policy.HolderId, details: ClaimEditDetails.Describe(Claim, update));

        Claim.Description = description;
        Claim.ClaimedAmount = amount;
        return this;
    }

    public SeedClaimBuilder Assign(TimeSpan after, User supervisor, User adjuster)
    {
        Advance(after);
        Ensure(supervisor.Role == Role.Supervisor, "Only supervisors assign claims."); // BR-04
        Ensure(adjuster.Role == Role.Adjuster, "The assignee must be an adjuster.");   // BR-04
        Ensure(ClaimLifecycle.CanBeAssigned(Claim.Status), $"A {Claim.Status} claim can't be assigned.");

        Claim.AssignedAdjusterId = adjuster.Id;
        Record(AuditAction.Assigned, supervisor.Id, details: $"Assigned to {adjuster.FullName}");
        return this;
    }

    public SeedClaimBuilder StartReview(TimeSpan after)
    {
        Advance(after);
        Ensure(Claim.AssignedAdjusterId is not null, "Only an assigned claim can be reviewed.");
        return Move(ClaimStatus.UnderReview, AuditAction.ReviewStarted, Claim.AssignedAdjusterId!.Value);
    }

    public SeedClaimBuilder Recommend(TimeSpan after, Recommendation recommendation, string notes)
    {
        Advance(after);
        Ensure(Claim.Status == ClaimStatus.UnderReview, "Recommendations need a claim UnderReview.");

        Claim.Recommendation = recommendation;
        Claim.AdjusterNotes = notes;
        Record(AuditAction.Recommended, Claim.AssignedAdjusterId!.Value, details: $"Recommended {recommendation}");
        return this;
    }

    public SeedClaimBuilder Decide(TimeSpan after, User supervisor, ClaimDecision decision, string? reason = null)
    {
        Advance(after);
        Ensure(supervisor.Role == Role.Supervisor, "Only supervisors decide claims.");
        Ensure(Claim.Recommendation is not null, "A recommendation is required before a decision.");
        Ensure(decision == ClaimDecision.Approve
               || reason?.Trim().Length >= DecideClaimRequestValidator.MinimumDenialReasonLength,
            "A denial needs a reason of at least 10 characters."); // BR-06

        var approve = decision == ClaimDecision.Approve;
        Claim.DecisionReason = reason;
        Claim.DecidedAt = _time;
        return Move(
            approve ? ClaimStatus.Approved : ClaimStatus.Denied,
            approve ? AuditAction.Approved : AuditAction.Denied,
            supervisor.Id,
            reason);
    }

    public SeedClaimBuilder Withdraw(TimeSpan after)
    {
        Advance(after);
        return Move(ClaimStatus.Withdrawn, AuditAction.Withdrawn, Claim.Policy.HolderId);
    }

    // BR-05: the same transition table as ClaimService.
    private SeedClaimBuilder Move(ClaimStatus newStatus, AuditAction action, Guid actorId, string? details = null)
    {
        Ensure(ClaimLifecycle.CanTransition(Claim.Status, newStatus),
            ClaimLifecycle.TransitionError(Claim.Status, newStatus));

        var oldStatus = Claim.Status;
        Claim.Status = newStatus;
        Record(action, actorId, oldStatus, newStatus, details);
        return this;
    }

    private void Record(
        AuditAction action, Guid actorId, ClaimStatus? fromStatus = null, ClaimStatus? toStatus = null, string? details = null)
    {
        _auditLog.Add(new AuditLogEntry
        {
            Claim = Claim,
            ActorUserId = actorId,
            Action = action,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Details = details,
            Timestamp = _time
        });
    }

    // Each step happens after the previous one, and never in the future.
    private void Advance(TimeSpan after)
    {
        _time += after;
        Ensure(_time <= _now, $"Seed step for {Claim.ClaimNumber} would be in the future.");
    }

    // BR-01 and BR-02, using the same checks as the API.
    private static void EnsureFitsPolicy(IClaimDetails details, Policy policy, DateTimeOffset at)
    {
        Ensure(details.ClaimedAmount > 0, "Claimed amount must be greater than 0.");
        Ensure(details.IncidentDate <= DateOnly.FromDateTime(at.UtcDateTime), "Incident can't be after the claim was filed.");
        var policyError = ClaimPolicyRules.Check(details, policy);
        Ensure(policyError is null, policyError ?? "");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Invalid seed data: {message}");
        }
    }
}
