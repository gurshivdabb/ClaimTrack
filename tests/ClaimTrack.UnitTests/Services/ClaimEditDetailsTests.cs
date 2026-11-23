using AwesomeAssertions;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Services;

namespace ClaimTrack.UnitTests.Services;

public class ClaimEditDetailsTests
{
    private static readonly Claim Before = new()
    {
        IncidentDate = new DateOnly(2026, 6, 1),
        Description = "Rear-ended at a red light",
        ClaimedAmount = 1_000m
    };

    private static UpdateClaimRequest Unchanged() => new(Before.IncidentDate, Before.Description, Before.ClaimedAmount);

    [Fact]
    public void Describe_AmountChanged_ShowsOldAndNewWithTwoDecimals()
    {
        ClaimEditDetails.Describe(Before, Unchanged() with { ClaimedAmount = 4_000m })
            .Should().Be("ClaimedAmount 1000.00 → 4000.00");
    }

    [Fact]
    public void Describe_DateChanged_ShowsOldAndNewDates()
    {
        ClaimEditDetails.Describe(Before, Unchanged() with { IncidentDate = new DateOnly(2026, 6, 2) })
            .Should().Be("IncidentDate 2026-06-01 → 2026-06-02");
    }

    [Fact]
    public void Describe_DescriptionChanged_SaysSoWithoutCopyingTheText()
    {
        ClaimEditDetails.Describe(Before, Unchanged() with { Description = "Rear-ended, bumper and trunk" })
            .Should().Be("Description changed");
    }

    [Fact]
    public void Describe_EverythingChanged_ListsAllChangesInOrder()
    {
        var after = new UpdateClaimRequest(new DateOnly(2026, 6, 2), "New text", 1_500m);

        ClaimEditDetails.Describe(Before, after).Should().Be(
            "ClaimedAmount 1000.00 → 1500.00; IncidentDate 2026-06-01 → 2026-06-02; Description changed");
    }

    [Fact]
    public void Describe_NothingChanged_SaysSo()
    {
        ClaimEditDetails.Describe(Before, Unchanged()).Should().Be("No fields changed");
    }
}
