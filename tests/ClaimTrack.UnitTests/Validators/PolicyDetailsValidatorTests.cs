using System.Globalization;
using ClaimTrack.Api.Dtos.Policies;
using ClaimTrack.Api.Enums;
using ClaimTrack.Api.Validators;
using FluentValidation.TestHelper;

namespace ClaimTrack.UnitTests.Validators;

public class PolicyDetailsValidatorTests
{
    private readonly PolicyDetailsValidator _validator = new();

    private static UpdatePolicyRequest ValidPolicy() => new(
        CoverageType.Auto,
        CoverageLimit: 10_000m,
        Deductible: 500m,
        StartDate: new DateOnly(2026, 1, 1),
        EndDate: new DateOnly(2026, 12, 31));

    private TestValidationResult<IPolicyDetails> Validate(IPolicyDetails policy) => _validator.TestValidate(policy);

    [Fact]
    public void Validate_ValidPolicy_HasNoErrors()
    {
        var result = Validate(ValidPolicy());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    public void Validate_CoverageLimitNotPositive_HasError(double coverageLimit)
    {
        var result = Validate(ValidPolicy() with { CoverageLimit = (decimal)coverageLimit });

        result.ShouldHaveValidationErrorFor(x => x.CoverageLimit);
    }

    // Money must fit decimal(18,2) exactly. Amounts are strings because C# attributes can't hold decimals.
    [Theory]
    [InlineData("100.12")]
    [InlineData("100.120")]
    public void Validate_CoverageLimitFitsDecimal18Scale2_HasNoError(string coverageLimit)
    {
        var result = Validate(ValidPolicy() with { CoverageLimit = decimal.Parse(coverageLimit, CultureInfo.InvariantCulture) });

        result.ShouldNotHaveValidationErrorFor(x => x.CoverageLimit);
    }

    [Theory]
    [InlineData("100.123")]
    [InlineData("99999999999999999.99")]
    public void Validate_CoverageLimitDoesNotFitDecimal18Scale2_HasError(string coverageLimit)
    {
        var result = Validate(ValidPolicy() with { CoverageLimit = decimal.Parse(coverageLimit, CultureInfo.InvariantCulture) });

        result.ShouldHaveValidationErrorFor(x => x.CoverageLimit);
    }

    [Fact]
    public void Validate_DeductibleWithThreeDecimals_HasError()
    {
        var result = Validate(ValidPolicy() with { Deductible = 0.001m });

        result.ShouldHaveValidationErrorFor(x => x.Deductible);
    }

    [Fact]
    public void Validate_DeductibleZero_HasNoError()
    {
        var result = Validate(ValidPolicy() with { Deductible = 0m });

        result.ShouldNotHaveValidationErrorFor(x => x.Deductible);
    }

    [Fact]
    public void Validate_DeductibleNegative_HasError()
    {
        var result = Validate(ValidPolicy() with { Deductible = -0.01m });

        result.ShouldHaveValidationErrorFor(x => x.Deductible);
    }

    [Fact]
    public void Validate_EndDateSameAsStartDate_HasError()
    {
        var sameDay = new DateOnly(2026, 1, 1);

        var result = Validate(ValidPolicy() with { StartDate = sameDay, EndDate = sameDay });

        result.ShouldHaveValidationErrorFor(x => x.EndDate)
            .WithErrorMessage("End date must be after the start date.");
    }

    [Fact]
    public void Validate_EndDateBeforeStartDate_HasError()
    {
        var result = Validate(ValidPolicy() with
        {
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 5, 31)
        });

        result.ShouldHaveValidationErrorFor(x => x.EndDate);
    }

    [Fact]
    public void Validate_UnknownCoverageType_HasError()
    {
        var result = Validate(ValidPolicy() with { CoverageType = (CoverageType)7 });

        result.ShouldHaveValidationErrorFor(x => x.CoverageType);
    }
}
