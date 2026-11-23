using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Enums;
using ClaimTrack.Api.Services;
using ClaimTrack.Api.Validators;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClaimTrack.Api.Data.Seed;

// Fills a development database with demo data (requirements §9).
// Safe to run on every startup: it only inserts what isn't there yet.
public class DatabaseSeeder(
    AppDbContext db,
    IPasswordHasher<User> passwordHasher,
    INumberGenerator numberGenerator,
    IOptions<SeedOptions> options,
    TimeProvider timeProvider,
    ILogger<DatabaseSeeder> logger)
{
    // ".test" is a domain reserved for testing, so these can never be real addresses.
    // Staff accounts can't be created through public registration (requirements §2).
    public static readonly IReadOnlyList<(string Email, string FullName, Role Role)> StaffAccounts =
    [
        ("supervisor@claimtrack.test", "Sarah Chen", Role.Supervisor),
        ("adjuster1@claimtrack.test", "Jane Doe", Role.Adjuster),
        ("adjuster2@claimtrack.test", "Raj Patel", Role.Adjuster)
    ];

    public static readonly IReadOnlyList<(string Email, string FullName, Role Role)> PolicyholderAccounts =
    [
        ("alex.martin@claimtrack.test", "Alex Martin", Role.Policyholder),
        ("priya.singh@claimtrack.test", "Priya Singh", Role.Policyholder),
        ("tom.wilson@claimtrack.test", "Tom Wilson", Role.Policyholder)
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var password = options.Value.DemoPassword;

        // Demo data is a convenience, so a missing password skips it instead of stopping the API.
        if (password.Length < RegisterRequestValidator.MinimumPasswordLength) // BR-11
        {
            logger.LogWarning(
                "Demo data was not seeded: set Seed:DemoPassword (at least {MinimumLength} characters) in user-secrets.",
                RegisterRequestValidator.MinimumPasswordLength);
            return;
        }

        await SeedAccountsAsync(StaffAccounts.Concat(PolicyholderAccounts), password, cancellationToken);
        await SeedPoliciesAndClaimsAsync(cancellationToken);
    }

    private async Task SeedAccountsAsync(
        IEnumerable<(string Email, string FullName, Role Role)> accounts, string password, CancellationToken cancellationToken)
    {
        var emails = accounts.Select(a => a.Email).ToList();
        var existingEmails = await db.Users
            .Where(u => emails.Contains(u.Email))
            .Select(u => u.Email)
            .ToListAsync(cancellationToken);

        foreach (var (email, fullName, role) in accounts.Where(a => !existingEmails.Contains(a.Email)))
        {
            var user = new User
            {
                Email = email,
                FullName = fullName,
                Role = role,
                CreatedAt = timeProvider.GetUtcNow().AddYears(-2) // accounts older than the demo policies
            };
            user.PasswordHash = passwordHasher.HashPassword(user, password); // BR-11: only the hash is stored

            db.Users.Add(user);
            logger.LogInformation("Seeded {Role} account {Email}.", role, email);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    // 5 policies and 10 claims covering every status, each with a matching audit trail.
    // Dates are relative to today, so the data always looks current and passes BR-02.
    private async Task SeedPoliciesAndClaimsAsync(CancellationToken cancellationToken)
    {
        var demoEmails = StaffAccounts.Concat(PolicyholderAccounts).Select(a => a.Email).ToList();
        var users = await db.Users
            .Where(u => demoEmails.Contains(u.Email))
            .ToDictionaryAsync(u => u.Email, cancellationToken);

        var holderIds = PolicyholderAccounts.Select(a => users[a.Email].Id).ToList();
        if (await db.Policies.AnyAsync(p => holderIds.Contains(p.HolderId), cancellationToken))
        {
            return; // already seeded
        }

        var supervisor = users["supervisor@claimtrack.test"];
        var adjuster1 = users["adjuster1@claimtrack.test"];
        var adjuster2 = users["adjuster2@claimtrack.test"];
        var alex = users["alex.martin@claimtrack.test"];
        var priya = users["priya.singh@claimtrack.test"];
        var tom = users["tom.wilson@claimtrack.test"];

        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var day = TimeSpan.FromDays(1);

        // Policies
        var alexAuto = await PolicyAsync(alex, CoverageType.Auto, 25_000m, 500m, today.AddDays(-180), today.AddDays(185), cancellationToken);
        var alexHome = await PolicyAsync(alex, CoverageType.Home, 350_000m, 1_000m, today.AddDays(-300), today.AddDays(65), cancellationToken);
        var priyaAuto = await PolicyAsync(priya, CoverageType.Auto, 30_000m, 750m, today.AddDays(-90), today.AddDays(275), cancellationToken);
        var priyaTravel = await PolicyAsync(priya, CoverageType.Travel, 10_000m, 100m, today.AddDays(-60), today.AddDays(30), cancellationToken);
        // Expired: ended months ago. Its one claim was filed while it was still active.
        var tomHome = await PolicyAsync(tom, CoverageType.Home, 400_000m, 2_500m, today.AddDays(-500), today.AddDays(-135), cancellationToken);

        async Task<SeedClaimBuilder> SubmitAsync(Policy policy, DateOnly incident, string description, decimal amount, DateTimeOffset submittedAt) =>
            SeedClaimBuilder.Submit(policy, await numberGenerator.NextClaimNumberAsync(cancellationToken),
                incident, description, amount, submittedAt, now);

        var expiredPolicyEnd = new DateTimeOffset(tomHome.EndDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var claims = new List<SeedClaimBuilder>
        {
            // 1. Submitted: just filed, not assigned yet.
            await SubmitAsync(priyaAuto, today.AddDays(-2), "Windshield cracked by a rock on the highway.", 450m, now - 1 * day),

            // 2. Submitted: assigned, review not started.
            (await SubmitAsync(alexAuto, today.AddDays(-6), "Side mirror broken in a parking lot.", 380m, now - 5 * day))
                .Assign(day, supervisor, adjuster1),

            // 3. Submitted: edited after filing (amount raised), so the history shows an Updated entry.
            (await SubmitAsync(priyaTravel, today.AddDays(-10), "Lost luggage on the return flight.", 800m, now - 8 * day))
                .Edit(day, "Lost luggage on the return flight. Receipts for replacement clothing attached.", 1_200m),

            // 4. UnderReview: review started, no recommendation yet.
            (await SubmitAsync(alexHome, today.AddDays(-20), "Water damage from a burst pipe under the kitchen sink.", 6_200m, now - 18 * day))
                .Assign(day, supervisor, adjuster2)
                .StartReview(2 * day),

            // 5. UnderReview: recommended, waiting for the supervisor's decision.
            (await SubmitAsync(priyaAuto, today.AddDays(-30), "Rear bumper damaged while parked.", 2_350m, now - 28 * day))
                .Assign(day, supervisor, adjuster1)
                .StartReview(day)
                .Recommend(3 * day, Recommendation.Approve, "Repair estimate from an approved shop matches the photos."),

            // 6. Approved: the full path.
            (await SubmitAsync(alexAuto, today.AddDays(-60), "Hail damage to the hood and roof.", 4_800m, now - 58 * day))
                .Assign(day, supervisor, adjuster1)
                .StartReview(day)
                .Recommend(4 * day, Recommendation.Approve, "Hail storm confirmed for that date; damage consistent with photos.")
                .Decide(2 * day, supervisor, ClaimDecision.Approve),

            // 7. Approved: on the expired policy, filed and decided while it was still active.
            (await SubmitAsync(tomHome, tomHome.EndDate.AddDays(-40), "Roof shingles torn off in a windstorm.", 9_500m, expiredPolicyEnd - 38 * day))
                .Assign(day, supervisor, adjuster2)
                .StartReview(day)
                .Recommend(5 * day, Recommendation.Approve, "Storm damage confirmed by the roofing contractor's report.")
                .Decide(2 * day, supervisor, ClaimDecision.Approve, "Covered windstorm damage; contractor invoice verified."),

            // 8. Denied: recommended Deny, denied with a reason.
            (await SubmitAsync(alexHome, today.AddDays(-45), "Crack in the basement wall with water seepage.", 12_000m, now - 43 * day))
                .Assign(day, supervisor, adjuster2)
                .StartReview(2 * day)
                .Recommend(5 * day, Recommendation.Deny, "Inspection shows long-term wear, which the policy excludes.")
                .Decide(2 * day, supervisor, ClaimDecision.Deny, "Gradual deterioration is not covered by the home policy."),

            // 9. Denied: reassigned once; the supervisor denies against an Approve recommendation.
            (await SubmitAsync(priyaTravel, today.AddDays(-25), "Trip cancelled; non-refundable hotel booking.", 1_900m, now - 24 * day))
                .Assign(day, supervisor, adjuster1)
                .Assign(2 * day, supervisor, adjuster2)
                .StartReview(day)
                .Recommend(2 * day, Recommendation.Approve, "Cancellation documents look genuine.")
                .Decide(day, supervisor, ClaimDecision.Deny, "Cancellation was due to a pre-existing condition, which is excluded."),

            // 10. Withdrawn: filed, then withdrawn by the policyholder.
            (await SubmitAsync(priyaAuto, today.AddDays(-15), "Scratch on the driver's door.", 300m, now - 14 * day))
                .Withdraw(2 * day)
        };

        db.Claims.AddRange(claims.Select(c => c.Claim));
        db.AuditLog.AddRange(claims.SelectMany(c => c.AuditLog));

        // One SaveChanges: all demo policies, claims, and audit entries are saved together, or none are.
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded 5 demo policies and {ClaimCount} demo claims.", claims.Count);
    }

    private async Task<Policy> PolicyAsync(
        User holder, CoverageType coverage, decimal limit, decimal deductible, DateOnly start, DateOnly end,
        CancellationToken cancellationToken)
    {
        var policy = new Policy
        {
            PolicyNumber = await numberGenerator.NextPolicyNumberAsync(cancellationToken),
            HolderId = holder.Id,
            CoverageType = coverage,
            CoverageLimit = limit,
            Deductible = deductible,
            StartDate = start,
            EndDate = end,
            CreatedAt = new DateTimeOffset(start.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
        };
        db.Policies.Add(policy);
        return policy;
    }
}
