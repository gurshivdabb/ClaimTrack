using ClaimTrack.Api.Dtos.Users;
using ClaimTrack.Api.Enums;
using ClaimTrack.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimTrack.Api.Controllers;

// Supervisors only: they assign claims to adjusters and create policies for policyholders, so they
// need to see who's who. Everyone else gets 403.
[ApiController]
[Route("api/users")]
public class UsersController(IUserService userService) : ControllerBase
{
    // e.g. GET /api/users?role=Adjuster
    [HttpGet]
    [Authorize(Roles = nameof(Role.Supervisor))]
    public async Task<ActionResult<List<UserSummaryResponse>>> GetAll(
        [FromQuery] Role? role, CancellationToken cancellationToken)
    {
        return await userService.GetAllAsync(role, cancellationToken);
    }
}
