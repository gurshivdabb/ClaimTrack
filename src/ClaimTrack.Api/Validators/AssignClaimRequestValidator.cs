using ClaimTrack.Api.Dtos.Claims;
using FluentValidation;

namespace ClaimTrack.Api.Validators;

// Input only. Whether the ID belongs to a real adjuster needs the database, so the service checks it (BR-04).
public class AssignClaimRequestValidator : AbstractValidator<AssignClaimRequest>
{
    public AssignClaimRequestValidator()
    {
        RuleFor(x => x.AdjusterId).NotEmpty();
    }
}
