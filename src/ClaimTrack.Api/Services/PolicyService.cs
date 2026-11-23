using ClaimTrack.Api.Common;
using ClaimTrack.Api.Data;
using ClaimTrack.Api.Dtos.Policies;
using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClaimTrack.Api.Services;

public class PolicyService(
    AppDbContext db, INumberGenerator numberGenerator, TimeProvider timeProvider, ICurrentUser currentUser)
    : IPolicyService
{
    public async Task<List<PolicyResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await db.Policies.VisibleTo(currentUser) // BR-09
            .AsNoTracking()
            .OrderBy(p => p.PolicyNumber)
            .Select(p => p.ToResponse())
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<PolicyResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var policy = await db.Policies.VisibleTo(currentUser) // BR-09
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return policy is null
            ? Result<PolicyResponse>.NotFound()
            : Result<PolicyResponse>.Ok(policy.ToResponse());
    }

    public async Task<Result<PolicyResponse>> CreateAsync(
        CreatePolicyRequest request, CancellationToken cancellationToken)
    {
        // A policy can only belong to an existing user with the Policyholder role.
        // Checked before taking a policy number, so a rejected request doesn't use one up.
        var holderIsPolicyholder = await db.Users.AnyAsync(
            u => u.Id == request.HolderId && u.Role == Role.Policyholder, cancellationToken);
        if (!holderIsPolicyholder)
        {
            return Result<PolicyResponse>.Invalid("Holder must be an existing policyholder.");
        }

        var policy = new Policy
        {
            PolicyNumber = await numberGenerator.NextPolicyNumberAsync(cancellationToken),
            HolderId = request.HolderId,
            CoverageType = request.CoverageType,
            CoverageLimit = request.CoverageLimit,
            Deductible = request.Deductible,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CreatedAt = timeProvider.GetUtcNow()
        };

        db.Policies.Add(policy);
        await db.SaveChangesAsync(cancellationToken);

        return Result<PolicyResponse>.Ok(policy.ToResponse());
    }

    public async Task<Result<PolicyResponse>> UpdateAsync(
        Guid id, UpdatePolicyRequest request, CancellationToken cancellationToken)
    {
        var policy = await db.Policies.VisibleTo(currentUser) // BR-09
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (policy is null)
        {
            return Result<PolicyResponse>.NotFound();
        }

        policy.CoverageType = request.CoverageType;
        policy.CoverageLimit = request.CoverageLimit;
        policy.Deductible = request.Deductible;
        policy.StartDate = request.StartDate;
        policy.EndDate = request.EndDate;

        await db.SaveChangesAsync(cancellationToken);

        return Result<PolicyResponse>.Ok(policy.ToResponse());
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var policy = await db.Policies.VisibleTo(currentUser) // BR-09
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (policy is null)
        {
            return Result.NotFound();
        }

        // Requirements §6: a policy can only be deleted if it has no claims.
        var hasClaims = await db.Claims.AnyAsync(c => c.PolicyId == id, cancellationToken);
        if (hasClaims)
        {
            return Result.Conflict("This policy has claims and cannot be deleted.");
        }

        db.Policies.Remove(policy);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
