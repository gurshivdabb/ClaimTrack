using ClaimTrack.Api.Dtos.Auth;
using ClaimTrack.Api.Entities;

namespace ClaimTrack.Api.Services;

public interface ITokenService
{
    LoginResponse CreateToken(User user);
}
