using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Enums;
using FluentValidation;

namespace ClaimTrack.Api.Validators;

public class DecideClaimRequestValidator : AbstractValidator<DecideClaimRequest>
{
    public const int MinimumDenialReasonLength = 10;

    public DecideClaimRequestValidator()
    {
        RuleFor(x => x.Decision)
            .NotNull()
            .IsInEnum();

        // BR-06: denying a claim needs a reason of at least 10 characters.
        // Trimmed, so ten spaces don't count as a reason.
        RuleFor(x => x.Reason)
            .Must(reason => reason is not null && reason.Trim().Length >= MinimumDenialReasonLength)
            .When(x => x.Decision == ClaimDecision.Deny)
            .WithMessage($"A reason of at least {MinimumDenialReasonLength} characters is required to deny a claim.");

        RuleFor(x => x.Reason)
            .MaximumLength(2000);
    }
}
