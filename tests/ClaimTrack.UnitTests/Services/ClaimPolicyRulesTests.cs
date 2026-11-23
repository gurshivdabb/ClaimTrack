using AwesomeAssertions;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Enums;
using ClaimTrack.Api.Services;

namespace ClaimTrack.UnitTests.Services;

public class ClaimPolicyRulesTests
{
    private static readonly DateOnly PolicyStart = new(2026, 1, 1);
    private static readonly DateOnly PolicyEnd = new(2026, 12, 31);

    private static readonly Policy Policy = new()
    {
        CoverageType = CoverageType.Auto,
        CoverageLimit = 5_000m,
        Deductible = 250m,
        StartDate = PolicyStart,
        EndDate = PolicyEnd
    };

    private static UpdateClaimRequest Claim(DateOnly incidentDate, decimal amount) =>
        new(incidentDate, "Fender bender", amount);

    [Fact]
    public void Check_ClaimWithinPolicy_ReturnsNull()
    {
        var error = ClaimPolicyRules.Check(Claim(new DateOnly(2026, 6, 1), 1_000m), Policy);

        error.Should().BeNull();
    }

    // BR-01: no more than the coverage limit.
    [Fact]
    public void Check_AmountExactlyAtLimit_ReturnsNull()
    {
        var error = ClaimPolicyRules.Check(Claim(new DateOnly(2026, 6, 1), 5_000m), Policy);

        error.Should().BeNull();
    }

    [Fact]
    public void Check_AmountOneCentOverLimit_ReturnsCoverageLimitError()
    {
        var error = ClaimPolicyRules.Check(Claim(new DateOnly(2026, 6, 1), 5_000.01m), Policy);

        error.Should().Be("Claimed amount exceeds the policy's coverage limit of 5000.00.");
    }

    // BR-02: the incident must happen while the policy is active (both end dates included).
    [Fact]
    public void Check_IncidentOnStartDate_ReturnsNull()
    {
        var error = ClaimPolicyRules.Check(Claim(PolicyStart, 1_000m), Policy);

        error.Should().BeNull();
    }

    [Fact]
    public void Check_IncidentOnEndDate_ReturnsNull()
    {
        var error = ClaimPolicyRules.Check(Claim(PolicyEnd, 1_000m), Policy);

        error.Should().BeNull();
    }

    [Fact]
    public void Check_IncidentDayBeforeStart_ReturnsDateError()
    {
        var error = ClaimPolicyRules.Check(Claim(PolicyStart.AddDays(-1), 1_000m), Policy);

        error.Should().Be("Incident date must be between 2026-01-01 and 2026-12-31.");
    }

    [Fact]
    public void Check_IncidentDayAfterEnd_ReturnsDateError()
    {
        var error = ClaimPolicyRules.Check(Claim(PolicyEnd.AddDays(1), 1_000m), Policy);

        error.Should().Be("Incident date must be between 2026-01-01 and 2026-12-31.");
    }

    [Fact]
    public void Check_BothRulesBroken_ReportsCoverageLimitFirst()
    {
        var error = ClaimPolicyRules.Check(Claim(PolicyEnd.AddDays(1), 9_999m), Policy);

        error.Should().StartWith("Claimed amount exceeds");
    }
}
