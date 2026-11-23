using ClaimTrack.Api.Enums;

namespace ClaimTrack.Api.Dtos.Policies;

public record PolicyResponse(
    Guid Id,
    string PolicyNumber,
    Guid HolderId,
    CoverageType CoverageType,
    decimal CoverageLimit,
    decimal Deductible,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTimeOffset CreatedAt);
