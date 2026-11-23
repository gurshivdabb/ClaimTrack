using ClaimTrack.Api.Dtos.Auth;
using ClaimTrack.Api.Services;
using ClaimTrack.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace ClaimTrack.Api.Controllers;

// The only public controller: you can't need a token to get a token.
// Rate limited per client IP (5 a minute by default), so passwords can't be guessed at full speed.
[AllowAnonymous]
[EnableRateLimiting(RateLimitingOptions.AuthPolicy)]
[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    // 201 Created without a Location header: there's no "get user" endpoint to point to.
    [HttpPost("register")]
    public async Task<ActionResult<UserResponse>> Register(
        RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);
        return result.ToActionResult(this, user => StatusCode(StatusCodes.Status201Created, user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        return result.ToActionResult(this);
    }
}
