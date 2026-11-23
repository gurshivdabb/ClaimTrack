using ClaimTrack.Api.Dtos.Auth;
using FluentValidation;

namespace ClaimTrack.Api.Validators;

// Only checks that both fields were sent. Format and length rules belong to registration;
// repeating them here would tell an attacker which part of a guess was wrong.
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}
