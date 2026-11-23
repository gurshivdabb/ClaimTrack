using AwesomeAssertions;
using ClaimTrack.Api.Data.Seed;
using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Enums;
using static ClaimTrack.UnitTests.TestClock;

namespace ClaimTrack.UnitTests.Services;

// The seed builder must refuse histories the API itself would refuse.
public class SeedClaimBuilderTests
{
    private static readonly TimeSpan Day = TimeSpan.FromDays(1);

    private readonly User _supervisor = new() { Id = Guid.NewGuid(), FullName = "Sarah Chen", Role = Role.Supervisor };
    private readonly User _adjuster = new() { Id = Guid.NewGuid(), FullName = "Jane Doe", Role = Role.Adjuster };
    private readonly Policy _policy = new()
    {
        Id = Guid.NewGuid(),
        HolderId = Guid.NewGuid(),
        CoverageLimit = 5_000m,
        StartDate = Today.AddDays(-100),
        EndDate = Today.AddDays(100)
    };

    private SeedClaimBuilder Submit(decimal amount = 1_000m, int daysAgo = 10) =>
        SeedClaimBuilder.Submit(_policy, "CLM-TEST", Today.AddDays(-daysAgo - 1), "Test claim", amount, Now - daysAgo * Day, Now);

    [Fact]
    public void FullPath_ProducesMatchingClaimAndAuditTrail()
    {
        var builder = Submit()
            .Assign(Day, _supervisor, _adjuster)
            .StartReview(Day)
            .Recommend(Day, Recommendation.Deny, "Not covered.")
            .Decide(Day, _supervisor, ClaimDecision.Deny, "Excluded under the policy terms.");

        builder.Claim.Status.Should().Be(ClaimStatus.Denied);
        builder.Claim.DecidedAt.Should().Be(Now - 6 * Day);
        builder.AuditLog.Select(a => a.Action).Should().Equal(
            AuditAction.Submitted, AuditAction.Assigned, AuditAction.ReviewStarted, AuditAction.Recommended, AuditAction.Denied);
        builder.AuditLog[1].Details.Should().Be("Assigned to Jane Doe");
    }

    [Fact]
    public void Decide_WithoutRecommendation_Throws()
    {
        var act = () => Submit().Assign(Day, _supervisor, _adjuster).StartReview(Day)
            .Decide(Day, _supervisor, ClaimDecision.Approve);

        act.Should().Throw<InvalidOperationException>().WithMessage("*recommendation*");
    }

    [Fact]
    public void Withdraw_AfterReviewStarted_Throws()
    {
        var act = () => Submit().Assign(Day, _supervisor, _adjuster).StartReview(Day).Withdraw(Day);

        act.Should().Throw<InvalidOperationException>().WithMessage("*UnderReview to Withdrawn*"); // BR-05
    }

    [Fact]
    public void Deny_WithShortReason_Throws()
    {
        var act = () => Submit().Assign(Day, _supervisor, _adjuster).StartReview(Day)
            .Recommend(Day, Recommendation.Deny, "Notes").Decide(Day, _supervisor, ClaimDecision.Deny, "Too short");

        act.Should().Throw<InvalidOperationException>(); // BR-06
    }

    [Fact]
    public void Assign_ToNonAdjuster_Throws()
    {
        var act = () => Submit().Assign(Day, _supervisor, _supervisor);

        act.Should().Throw<InvalidOperationException>(); // BR-04
    }

    [Fact]
    public void Submit_AmountOverCoverageLimit_Throws()
    {
        var act = () => Submit(amount: 5_000.01m);

        act.Should().Throw<InvalidOperationException>().WithMessage("*coverage limit*"); // BR-01
    }

    [Fact]
    public void Step_EndingInTheFuture_Throws()
    {
        var act = () => Submit(daysAgo: 1).Assign(2 * Day, _supervisor, _adjuster);

        act.Should().Throw<InvalidOperationException>().WithMessage("*future*");
    }
}
