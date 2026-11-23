using ClaimTrack.Api.Dtos.Policies;
using FluentValidation;

namespace ClaimTrack.Api.Validators;

// Rules shared by CreatePolicyRequest and UpdatePolicyRequest.
public class PolicyDetailsValidator : AbstractValidator<IPolicyDetails>
{
    public PolicyDetailsValidator()
    {
        RuleFor(x => x.CoverageType).IsInEnum();

        // Money must fit the decimal(18,2) columns exactly, so the database never rounds a value
        // or rejects one that's too large. Trailing zeros (100.10) are fine.
        RuleFor(x => x.CoverageLimit)
            .GreaterThan(0)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(x => x.Deductible)
            .GreaterThanOrEqualTo(0)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithMessage("End date must be after the start date.");
    }
}
