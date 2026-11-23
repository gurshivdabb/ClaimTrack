using ClaimTrack.Api.Dtos.Auth;
using FluentValidation;

namespace ClaimTrack.Api.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public const int MinimumPasswordLength = 8;

    public RegisterRequestValidator()
    {
        // The lengths match the Users table columns in UserConfiguration.
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        // BR-11
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(MinimumPasswordLength);

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200);
    }
}
