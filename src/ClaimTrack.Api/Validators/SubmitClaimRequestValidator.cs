using ClaimTrack.Api.Dtos.Claims;
using FluentValidation;

namespace ClaimTrack.Api.Validators;

public class SubmitClaimRequestValidator : AbstractValidator<SubmitClaimRequest>
{
    public SubmitClaimRequestValidator(IValidator<IClaimDetails> claimDetailsValidator)
    {
        Include(claimDetailsValidator);
        RuleFor(x => x.PolicyId).NotEmpty();
    }
}
