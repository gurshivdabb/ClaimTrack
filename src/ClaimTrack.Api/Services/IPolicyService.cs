using ClaimTrack.Api.Common;
using ClaimTrack.Api.Dtos.Policies;

namespace ClaimTrack.Api.Services;

public interface IPolicyService
{
    Task<List<PolicyResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<Result<PolicyResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<PolicyResponse>> CreateAsync(CreatePolicyRequest request, CancellationToken cancellationToken);
    Task<Result<PolicyResponse>> UpdateAsync(Guid id, UpdatePolicyRequest request, CancellationToken cancellationToken);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
