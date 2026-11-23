using ClaimTrack.Api.Common;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Enums;
using ClaimTrack.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimTrack.Api.Controllers;

// Every endpoint needs a logged-in user (RequireAuthorization in Program.cs).
// Actions marked [Authorize(Roles = ...)] also need that role, or the response is 403.

[ApiController]
[Route("api/claims")]
public class ClaimsController(IClaimService claimService, IAuditService auditService) : ControllerBase
{
    // e.g. GET /api/claims?status=UnderReview&page=1&pageSize=20&sort=-submittedAt
    [HttpGet]
    public async Task<ActionResult<PagedResult<ClaimResponse>>> GetAll(
        [FromQuery] ClaimListQuery query, CancellationToken cancellationToken)
    {
        return await claimService.GetAllAsync(query, cancellationToken);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClaimResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await claimService.GetByIdAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    // Any role: whoever can see the claim can see its history; others get 404 (BR-09).
    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<List<AuditLogEntryResponse>>> History(Guid id, CancellationToken cancellationToken)
    {
        var result = await auditService.GetHistoryAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    // Requirements §2: policyholders submit claims.
    [HttpPost]
    [Authorize(Roles = nameof(Role.Policyholder))]
    public async Task<ActionResult<ClaimResponse>> Submit(
        SubmitClaimRequest request, CancellationToken cancellationToken)
    {
        var result = await claimService.SubmitAsync(request, cancellationToken);
        return result.ToActionResult(this, claim => CreatedAtAction(nameof(GetById), new { id = claim.Id }, claim));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = nameof(Role.Policyholder))]
    public async Task<ActionResult<ClaimResponse>> Update(
        Guid id, UpdateClaimRequest request, CancellationToken cancellationToken)
    {
        var result = await claimService.UpdateAsync(id, request, cancellationToken);
        return result.ToActionResult(this);
    }

    // BR-04: only supervisors assign or reassign claims.
    [HttpPost("{id:guid}/assign")]
    [Authorize(Roles = nameof(Role.Supervisor))]
    public async Task<ActionResult<ClaimResponse>> Assign(
        Guid id, AssignClaimRequest request, CancellationToken cancellationToken)
    {
        var result = await claimService.AssignAsync(id, request, cancellationToken);
        return result.ToActionResult(this);
    }

    // Only the assigned adjuster: other adjusters can't see the claim, so they get 404 (BR-09).
    [HttpPost("{id:guid}/start-review")]
    [Authorize(Roles = nameof(Role.Adjuster))]
    public async Task<ActionResult<ClaimResponse>> StartReview(Guid id, CancellationToken cancellationToken)
    {
        var result = await claimService.StartReviewAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{id:guid}/recommendation")]
    [Authorize(Roles = nameof(Role.Adjuster))]
    public async Task<ActionResult<ClaimResponse>> Recommend(
        Guid id, RecommendClaimRequest request, CancellationToken cancellationToken)
    {
        var result = await claimService.RecommendAsync(id, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{id:guid}/decision")]
    [Authorize(Roles = nameof(Role.Supervisor))]
    public async Task<ActionResult<ClaimResponse>> Decide(
        Guid id, DecideClaimRequest request, CancellationToken cancellationToken)
    {
        var result = await claimService.DecideAsync(id, request, cancellationToken);
        return result.ToActionResult(this);
    }

    // Only the owning policyholder: others can't see the claim, so they get 404 (BR-09).
    [HttpPost("{id:guid}/withdraw")]
    [Authorize(Roles = nameof(Role.Policyholder))]
    public async Task<ActionResult<ClaimResponse>> Withdraw(Guid id, CancellationToken cancellationToken)
    {
        var result = await claimService.WithdrawAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    // No DELETE endpoint: claims are never hard-deleted (BR-12). A policyholder withdraws instead.
}
