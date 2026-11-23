using ClaimTrack.Api.Enums;

namespace ClaimTrack.Api.Dtos.Policies;

public record CreatePolicyRequest(
    Guid HolderId,
    CoverageType CoverageType,
    decimal CoverageLimit,
    decimal Deductible,
    DateOnly StartDate,
    DateOnly EndDate) : IPolicyDetails;
