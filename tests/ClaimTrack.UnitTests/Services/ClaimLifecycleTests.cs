using AwesomeAssertions;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Enums;
using ClaimTrack.Api.Services;
using ClaimTrack.Api.Validators;
using FluentValidation.TestHelper;

namespace ClaimTrack.UnitTests.Services;

public class ClaimLifecycleTests
{
    [Theory]
    [InlineData(ClaimStatus.Submitted)]
    [InlineData(ClaimStatus.UnderReview)]
    public void CanBeAssigned_OpenClaim_IsTrue(ClaimStatus status)
    {
        ClaimLifecycle.CanBeAssigned(status).Should().BeTrue();
    }

    [Theory]
    [InlineData(ClaimStatus.Approved)]
    [InlineData(ClaimStatus.Denied)]
    [InlineData(ClaimStatus.Withdrawn)]
    public void CanBeAssigned_FinalClaim_IsFalse(ClaimStatus status)
    {
        ClaimLifecycle.CanBeAssigned(status).Should().BeFalse(); // §4: final statuses don't change
    }

    // BR-05: every (from, to) pair; exactly the four in requirements §4 are allowed.
    public static TheoryData<ClaimStatus, ClaimStatus, bool> AllTransitions()
    {
        var allowed = new HashSet<(ClaimStatus, ClaimStatus)>
        {
            (ClaimStatus.Submitted, ClaimStatus.UnderReview),
            (ClaimStatus.Submitted, ClaimStatus.Withdrawn),
            (ClaimStatus.UnderReview, ClaimStatus.Approved),
            (ClaimStatus.UnderReview, ClaimStatus.Denied)
        };
        var data = new TheoryData<ClaimStatus, ClaimStatus, bool>();
        foreach (var from in Enum.GetValues<ClaimStatus>())
        foreach (var to in Enum.GetValues<ClaimStatus>())
        {
            data.Add(from, to, allowed.Contains((from, to)));
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(AllTransitions))]
    public void CanTransition_EveryPair_MatchesTheRequirementsTable(ClaimStatus from, ClaimStatus to, bool expected)
    {
        ClaimLifecycle.CanTransition(from, to).Should().Be(expected);
    }

    [Theory]
    [InlineData(ClaimStatus.Submitted, false)]
    [InlineData(ClaimStatus.UnderReview, false)]
    [InlineData(ClaimStatus.Approved, true)]
    [InlineData(ClaimStatus.Denied, true)]
    [InlineData(ClaimStatus.Withdrawn, true)]
    public void IsFinal_EachStatus_MatchesRequirements(ClaimStatus status, bool expected)
    {
        ClaimLifecycle.IsFinal(status).Should().Be(expected);
    }

    [Fact]
    public void TransitionError_NamesBothStatuses()
    {
        ClaimLifecycle.TransitionError(ClaimStatus.Approved, ClaimStatus.UnderReview)
            .Should().Be("Cannot move a claim from Approved to UnderReview.");
    }

    [Fact]
    public void AssignValidate_EmptyAdjusterId_HasError()
    {
        var result = new AssignClaimRequestValidator().TestValidate(new AssignClaimRequest(Guid.Empty));

        result.ShouldHaveValidationErrorFor(x => x.AdjusterId);
    }
}
