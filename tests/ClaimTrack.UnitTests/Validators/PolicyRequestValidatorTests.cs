using ClaimTrack.Api.Dtos.Policies;
using ClaimTrack.Api.Enums;
using ClaimTrack.Api.Validators;
using FluentValidation.TestHelper;

namespace ClaimTrack.UnitTests.Validators;

// The shared rules are tested in PolicyDetailsValidatorTests. These tests check each
// request's own rules, and that the shared rules are included.
public class PolicyRequestValidatorTests
{
    private readonly CreatePolicyRequestValidator _createValidator = new(new PolicyDetailsValidator());
    private readonly UpdatePolicyRequestValidator _updateValidator = new(new PolicyDetailsValidator());

    private static CreatePolicyRequest ValidCreateRequest() => new(
        HolderId: Guid.NewGuid(),
        CoverageType.Home,
        CoverageLimit: 300_000m,
        Deductible: 1_000m,
        StartDate: new DateOnly(2026, 1, 1),
        EndDate: new DateOnly(2026, 12, 31));

    [Fact]
    public void CreateValidate_ValidRequest_HasNoErrors()
    {
        var result = _createValidator.TestValidate(ValidCreateRequest());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateValidate_EmptyHolderId_HasError()
    {
        var result = _createValidator.TestValidate(ValidCreateRequest() with { HolderId = Guid.Empty });

        result.ShouldHaveValidationErrorFor(x => x.HolderId);
    }

    [Fact]
    public void CreateValidate_ZeroCoverageLimit_HasErrorFromSharedRules()
    {
        var result = _createValidator.TestValidate(ValidCreateRequest() with { CoverageLimit = 0m });

        result.ShouldHaveValidationErrorFor(x => x.CoverageLimit);
    }

    [Fact]
    public void UpdateValidate_EndDateBeforeStartDate_HasErrorFromSharedRules()
    {
        var request = new UpdatePolicyRequest(
            CoverageType.Travel,
            CoverageLimit: 5_000m,
            Deductible: 100m,
            StartDate: new DateOnly(2026, 6, 1),
            EndDate: new DateOnly(2026, 1, 1));

        var result = _updateValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.EndDate);
    }
}
