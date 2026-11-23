using ClaimTrack.Api.Dtos.Policies;
using ClaimTrack.Api.Enums;
using ClaimTrack.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimTrack.Api.Controllers;

// Every endpoint needs a logged-in user (RequireAuthorization in Program.cs).
// Actions marked [Authorize(Roles = ...)] also need that role, or the response is 403.

[ApiController]
[Route("api/policies")]
public class PoliciesController(IPolicyService policyService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PolicyResponse>>> GetAll(CancellationToken cancellationToken)
    {
        return await policyService.GetAllAsync(cancellationToken);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PolicyResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await policyService.GetByIdAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    // Requirements §2: supervisors create policies.
    [HttpPost]
    [Authorize(Roles = nameof(Role.Supervisor))]
    public async Task<ActionResult<PolicyResponse>> Create(
        CreatePolicyRequest request, CancellationToken cancellationToken)
    {
        var result = await policyService.CreateAsync(request, cancellationToken);
        return result.ToActionResult(this, policy => CreatedAtAction(nameof(GetById), new { id = policy.Id }, policy));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = nameof(Role.Supervisor))]
    public async Task<ActionResult<PolicyResponse>> Update(
        Guid id, UpdatePolicyRequest request, CancellationToken cancellationToken)
    {
        var result = await policyService.UpdateAsync(id, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = nameof(Role.Supervisor))]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await policyService.DeleteAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }
}
