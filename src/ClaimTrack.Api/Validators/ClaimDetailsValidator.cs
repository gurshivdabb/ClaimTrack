using ClaimTrack.Api.Dtos.Claims;
using FluentValidation;

namespace ClaimTrack.Api.Validators;

// Rules shared by SubmitClaimRequest and UpdateClaimRequest. Only checks that need the
// request alone; checks against the policy's data are in ClaimPolicyRules.
public class ClaimDetailsValidator : AbstractValidator<IClaimDetails>
{
    public ClaimDetailsValidator(TimeProvider timeProvider)
    {
        // BR-01 (first half): the amount must be positive. It must also fit the decimal(18,2)
        // column exactly, so the database never rounds it or rejects it as too large.
        RuleFor(x => x.ClaimedAmount)
            .GreaterThan(0)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);

        // BR-02 (first half): the incident can't be in the future.
        RuleFor(x => x.IncidentDate)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime))
            .WithMessage("Incident date cannot be in the future.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(2000);
    }
}
