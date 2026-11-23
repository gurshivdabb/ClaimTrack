using ClaimTrack.Api.Enums;

namespace ClaimTrack.Api.Entities;

public class Policy
{
    public Guid Id { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;

    // The Policyholder who owns this policy.
    public Guid HolderId { get; set; }
    public User Holder { get; set; } = null!;

    public CoverageType CoverageType { get; set; }
    public decimal CoverageLimit { get; set; }
    public decimal Deductible { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public List<Claim> Claims { get; set; } = [];
}
