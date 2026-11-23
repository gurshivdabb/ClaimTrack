using ClaimTrack.Api.Enums;

namespace ClaimTrack.Api.Services;

// The claim lifecycle from requirements §4, as pure rules with no database,
// so every rule can be unit-tested on its own.
public static class ClaimLifecycle
{
    // BR-05: the only status changes allowed. Anything not listed is rejected with 409.
    // Who may make each change is enforced by roles and visibility (BR-09), not here.
    private static readonly HashSet<(ClaimStatus From, ClaimStatus To)> AllowedTransitions =
    [
        (ClaimStatus.Submitted, ClaimStatus.UnderReview), // start review: assigned adjuster
        (ClaimStatus.Submitted, ClaimStatus.Withdrawn),   // withdraw: owning policyholder
        (ClaimStatus.UnderReview, ClaimStatus.Approved),  // decision: supervisor
        (ClaimStatus.UnderReview, ClaimStatus.Denied)     // decision: supervisor
    ];

    public static bool CanTransition(ClaimStatus from, ClaimStatus to) => AllowedTransitions.Contains((from, to));

    // A final status is one with no way out: Approved, Denied, Withdrawn.
    // Worked out from the table, so the list of final statuses is never written twice.
    public static bool IsFinal(ClaimStatus status) => !AllowedTransitions.Any(t => t.From == status);

    // §4: assignment can happen while Submitted or UnderReview, i.e. any status that isn't final.
    public static bool CanBeAssigned(ClaimStatus status) => !IsFinal(status);

    public static string TransitionError(ClaimStatus from, ClaimStatus to) =>
        $"Cannot move a claim from {from} to {to}.";
}
