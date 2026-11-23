using System.Globalization;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Validators;
using FluentValidation.TestHelper;
using static ClaimTrack.UnitTests.TestClock;

namespace ClaimTrack.UnitTests.Validators;

public class ClaimDetailsValidatorTests
{
    private readonly ClaimDetailsValidator _validator = new(CreateTimeProvider());

    private static UpdateClaimRequest ValidClaim() => new(
        IncidentDate: Today,
        Description: "Rear-ended at a red light",
        ClaimedAmount: 2_500m);

    private TestValidationResult<IClaimDetails> Validate(IClaimDetails claim) => _validator.TestValidate(claim);

    [Fact]
    public void Validate_ValidClaim_HasNoErrors()
    {
        var result = Validate(ValidClaim());

        result.ShouldNotHaveAnyValidationErrors();
    }

    // BR-01 (first half): the amount must be greater than 0.
    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Validate_ClaimedAmountNotPositive_HasError(double claimedAmount)
    {
        var result = Validate(ValidClaim() with { ClaimedAmount = (decimal)claimedAmount });

        result.ShouldHaveValidationErrorFor(x => x.ClaimedAmount);
    }

    [Fact]
    public void Validate_ClaimedAmountOneCent_HasNoError()
    {
        var result = Validate(ValidClaim() with { ClaimedAmount = 0.01m });

        result.ShouldNotHaveValidationErrorFor(x => x.ClaimedAmount);
    }

    // Money must fit decimal(18,2) exactly. Amounts are strings because C# attributes can't hold decimals.
    [Theory]
    [InlineData("100.13")]
    [InlineData("100.130")]
    [InlineData("9999999999999999.99")]
    public void Validate_ClaimedAmountFitsDecimal18Scale2_HasNoError(string claimedAmount)
    {
        var result = Validate(ValidClaim() with { ClaimedAmount = decimal.Parse(claimedAmount, CultureInfo.InvariantCulture) });

        result.ShouldNotHaveValidationErrorFor(x => x.ClaimedAmount);
    }

    [Theory]
    [InlineData("100.129")]
    [InlineData("99999999999999999.99")]
    public void Validate_ClaimedAmountDoesNotFitDecimal18Scale2_HasError(string claimedAmount)
    {
        var result = Validate(ValidClaim() with { ClaimedAmount = decimal.Parse(claimedAmount, CultureInfo.InvariantCulture) });

        result.ShouldHaveValidationErrorFor(x => x.ClaimedAmount);
    }

    // BR-02 (first half): the incident can't be in the future.
    [Fact]
    public void Validate_IncidentToday_HasNoError()
    {
        var result = Validate(ValidClaim() with { IncidentDate = Today });

        result.ShouldNotHaveValidationErrorFor(x => x.IncidentDate);
    }

    [Fact]
    public void Validate_IncidentTomorrow_HasError()
    {
        var result = Validate(ValidClaim() with { IncidentDate = Today.AddDays(1) });

        result.ShouldHaveValidationErrorFor(x => x.IncidentDate)
            .WithErrorMessage("Incident date cannot be in the future.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_DescriptionBlank_HasError(string description)
    {
        var result = Validate(ValidClaim() with { Description = description });

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_Description2000Characters_HasNoError()
    {
        var result = Validate(ValidClaim() with { Description = new string('x', 2000) });

        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_Description2001Characters_HasError()
    {
        var result = Validate(ValidClaim() with { Description = new string('x', 2001) });

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }
}
