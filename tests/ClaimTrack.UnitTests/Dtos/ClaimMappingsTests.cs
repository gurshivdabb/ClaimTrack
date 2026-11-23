using AwesomeAssertions;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Enums;

namespace ClaimTrack.UnitTests.Dtos;

public class ClaimMappingsTests
{
    [Fact]
    public void ToResponse_DecidedClaim_CopiesEveryField()
    {
        // All optional fields are filled in, so a field that isn't copied would show up as null.
        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            ClaimNumber = "CLM-2026-00001",
            PolicyId = Guid.NewGuid(),
            IncidentDate = new DateOnly(2026, 3, 1),
            Description = "Hail damage to the roof",
            ClaimedAmount = 12_500.50m,
            Status = ClaimStatus.Denied,
            AssignedAdjusterId = Guid.NewGuid(),
            Recommendation = Recommendation.Deny,
            AdjusterNotes = "Damage pre-dates the policy.",
            DecisionReason = "Damage occurred before coverage started.",
            SubmittedAt = new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.Zero),
            DecidedAt = new DateTimeOffset(2026, 3, 20, 15, 30, 0, TimeSpan.Zero)
        };

        var response = claim.ToResponse();

        // Every property on the response must match the property with the same name on the
        // entity. Entity-only members (like the Policy navigation) are ignored.
        response.Should().BeEquivalentTo(claim, options => options.ExcludingMissingMembers());
    }
}
