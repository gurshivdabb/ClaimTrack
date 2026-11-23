using ClaimTrack.Api.Dtos.Policies;
using FluentValidation;

namespace ClaimTrack.Api.Validators;

public class UpdatePolicyRequestValidator : AbstractValidator<UpdatePolicyRequest>
{
    public UpdatePolicyRequestValidator(IValidator<IPolicyDetails> policyDetailsValidator)
    {
        Include(policyDetailsValidator);
    }
}
