using ClaimTrack.Api.Dtos.Policies;
using FluentValidation;

namespace ClaimTrack.Api.Validators;

public class CreatePolicyRequestValidator : AbstractValidator<CreatePolicyRequest>
{
    public CreatePolicyRequestValidator(IValidator<IPolicyDetails> policyDetailsValidator)
    {
        Include(policyDetailsValidator);
        RuleFor(x => x.HolderId).NotEmpty();
    }
}
