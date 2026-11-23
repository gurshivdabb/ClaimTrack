using ClaimTrack.Api.Entities;

namespace ClaimTrack.Api.Dtos.Policies;

public static class PolicyMappings
{
    public static PolicyResponse ToResponse(this Policy policy) => new(
        policy.Id,
        policy.PolicyNumber,
        policy.HolderId,
        policy.CoverageType,
        policy.CoverageLimit,
        policy.Deductible,
        policy.StartDate,
        policy.EndDate,
        policy.CreatedAt);
}
