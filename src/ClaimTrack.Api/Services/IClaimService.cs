using ClaimTrack.Api.Common;
using ClaimTrack.Api.Dtos.Claims;

namespace ClaimTrack.Api.Services;

public interface IClaimService
{
    Task<PagedResult<ClaimResponse>> GetAllAsync(ClaimListQuery query, CancellationToken cancellationToken);
    Task<Result<ClaimResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<ClaimResponse>> SubmitAsync(SubmitClaimRequest request, CancellationToken cancellationToken);
    Task<Result<ClaimResponse>> UpdateAsync(Guid id, UpdateClaimRequest request, CancellationToken cancellationToken);
    Task<Result<ClaimResponse>> AssignAsync(Guid id, AssignClaimRequest request, CancellationToken cancellationToken);
    Task<Result<ClaimResponse>> StartReviewAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<ClaimResponse>> RecommendAsync(Guid id, RecommendClaimRequest request, CancellationToken cancellationToken);
    Task<Result<ClaimResponse>> DecideAsync(Guid id, DecideClaimRequest request, CancellationToken cancellationToken);
    Task<Result<ClaimResponse>> WithdrawAsync(Guid id, CancellationToken cancellationToken);
}
