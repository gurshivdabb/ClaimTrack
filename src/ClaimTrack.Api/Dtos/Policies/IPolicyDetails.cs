using ClaimTrack.Api.Enums;

namespace ClaimTrack.Api.Dtos.Policies;

// The fields shared by create and update requests, so their rules are written once
// in PolicyDetailsValidator.
public interface IPolicyDetails
{
    CoverageType CoverageType { get; }
    decimal CoverageLimit { get; }
    decimal Deductible { get; }
    DateOnly StartDate { get; }
    DateOnly EndDate { get; }
}
