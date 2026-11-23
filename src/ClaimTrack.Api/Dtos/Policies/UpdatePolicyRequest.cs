using ClaimTrack.Api.Enums;

namespace ClaimTrack.Api.Dtos.Policies;

// The holder is not included: a policy can't be moved to a different person.
public record UpdatePolicyRequest(
    CoverageType CoverageType,
    decimal CoverageLimit,
    decimal Deductible,
    DateOnly StartDate,
    DateOnly EndDate) : IPolicyDetails;
