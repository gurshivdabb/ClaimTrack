using AwesomeAssertions;
using ClaimTrack.Api.Dtos.Policies;
using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Enums;

namespace ClaimTrack.UnitTests.Dtos;

public class PolicyMappingsTests
{
    [Fact]
    public void ToResponse_Policy_CopiesEveryField()
    {
        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-2026-00001",
            HolderId = Guid.NewGuid(),
            CoverageType = CoverageType.Travel,
            CoverageLimit = 10_000m,
            Deductible = 250m,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            CreatedAt = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero)
        };

        var response = policy.ToResponse();

        // Every property on the response must match the property with the same name on the
        // entity. Entity-only members (like the Claims list) are ignored.
        response.Should().BeEquivalentTo(policy, options => options.ExcludingMissingMembers());
    }
}
