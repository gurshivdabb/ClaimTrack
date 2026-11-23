using ClaimTrack.Api.Common;
using ClaimTrack.Api.Dtos.Auth;

namespace ClaimTrack.Api.Services;

public interface IAuthService
{
    Task<Result<UserResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}
