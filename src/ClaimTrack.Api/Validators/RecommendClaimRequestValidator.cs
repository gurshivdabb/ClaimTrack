using ClaimTrack.Api.Dtos.Claims;
using FluentValidation;

namespace ClaimTrack.Api.Validators;

public class RecommendClaimRequestValidator : AbstractValidator<RecommendClaimRequest>
{
    public RecommendClaimRequestValidator()
    {
        RuleFor(x => x.Recommendation)
            .NotNull()
            .IsInEnum();

        // Requirements §2: a recommendation comes "with notes", so the supervisor knows why.
        RuleFor(x => x.Notes)
            .NotEmpty()
            .MaximumLength(2000);
    }
}
