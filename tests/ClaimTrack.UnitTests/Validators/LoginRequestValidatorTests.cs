using AwesomeAssertions;
using ClaimTrack.Api.Dtos.Auth;
using ClaimTrack.Api.Validators;
using FluentValidation.TestHelper;

namespace ClaimTrack.UnitTests.Validators;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void Validate_EmailAndPassword_HasNoErrors()
    {
        var result = _validator.TestValidate(new LoginRequest("jane@example.com", "anything"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyFields_HasErrorForEach()
    {
        var result = _validator.TestValidate(new LoginRequest("", ""));

        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void ToString_LeavesOutThePassword()
    {
        var text = new LoginRequest("jane@example.com", "SuperSecret1!").ToString();

        text.Should().Contain("jane@example.com").And.NotContain("SuperSecret1!");
    }
}
