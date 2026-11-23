using ClaimTrack.Api.Dtos.Auth;
using ClaimTrack.Api.Validators;
using FluentValidation.TestHelper;

namespace ClaimTrack.UnitTests.Validators;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    private static RegisterRequest ValidRequest() => new(
        Email: "jane@example.com",
        Password: "12345678",
        FullName: "Jane Doe");

    [Fact]
    public void Validate_ValidRequest_HasNoErrors()
    {
        var result = _validator.TestValidate(ValidRequest());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_PasswordOfSevenCharacters_HasError()
    {
        var result = _validator.TestValidate(ValidRequest() with { Password = "1234567" });

        result.ShouldHaveValidationErrorFor(x => x.Password); // BR-11
    }

    [Fact]
    public void Validate_PasswordOfEightCharacters_HasNoError()
    {
        var result = _validator.TestValidate(ValidRequest() with { Password = "12345678" });

        result.ShouldNotHaveValidationErrorFor(x => x.Password); // BR-11
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Validate_BadEmail_HasError(string email)
    {
        var result = _validator.TestValidate(ValidRequest() with { Email = email });

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_EmailLongerThanColumn_HasError()
    {
        var email = new string('a', 245) + "@example.com"; // 257 characters

        var result = _validator.TestValidate(ValidRequest() with { Email = email });

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyFullName_HasError(string fullName)
    {
        var result = _validator.TestValidate(ValidRequest() with { FullName = fullName });

        result.ShouldHaveValidationErrorFor(x => x.FullName);
    }
}
