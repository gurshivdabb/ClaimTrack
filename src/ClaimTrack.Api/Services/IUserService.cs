using ClaimTrack.Api.Dtos.Users;
using ClaimTrack.Api.Enums;

namespace ClaimTrack.Api.Services;

public interface IUserService
{
    // Users ordered by name, optionally only those with one role.
    Task<List<UserSummaryResponse>> GetAllAsync(Role? role, CancellationToken cancellationToken);
}
