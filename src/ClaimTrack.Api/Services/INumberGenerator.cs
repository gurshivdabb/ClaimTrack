namespace ClaimTrack.Api.Services;

public interface INumberGenerator
{
    Task<string> NextPolicyNumberAsync(CancellationToken cancellationToken);
    Task<string> NextClaimNumberAsync(CancellationToken cancellationToken);
}
