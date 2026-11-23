using ClaimTrack.Api.Dtos.Claims;
using FluentValidation;

namespace ClaimTrack.Api.Validators;

public class UpdateClaimRequestValidator : AbstractValidator<UpdateClaimRequest>
{
    public UpdateClaimRequestValidator(IValidator<IClaimDetails> claimDetailsValidator)
    {
        Include(claimDetailsValidator);
    }
}
