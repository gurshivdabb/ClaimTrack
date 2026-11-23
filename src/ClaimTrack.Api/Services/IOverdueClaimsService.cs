namespace ClaimTrack.Api.Services;

public interface IOverdueClaimsService
{
    // Flags every claim that has been UnderReview for too long and isn't flagged yet.
    // Returns how many claims were flagged by this run.
    Task<int> FlagOverdueClaimsAsync(CancellationToken cancellationToken);
}
