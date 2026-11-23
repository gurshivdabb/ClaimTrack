using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Enums;
using ClaimTrack.Api.Validators;
using FluentValidation.TestHelper;

namespace ClaimTrack.UnitTests.Validators;

public class WorkflowRequestValidatorTests
{
    private readonly RecommendClaimRequestValidator _recommendValidator = new();
    private readonly DecideClaimRequestValidator _decideValidator = new();

    [Fact]
    public void Recommend_ValidRequest_HasNoErrors()
    {
        var result = _recommendValidator.TestValidate(
            new RecommendClaimRequest(Recommendation.Deny, "Damage predates the policy start."));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Recommend_MissingRecommendation_HasError()
    {
        // A missing field must be rejected, never treated as the first enum value (Approve).
        var result = _recommendValidator.TestValidate(new RecommendClaimRequest(null, "Notes"));

        result.ShouldHaveValidationErrorFor(x => x.Recommendation);
    }

    [Fact]
    public void Recommend_UnknownRecommendation_HasError()
    {
        var result = _recommendValidator.TestValidate(new RecommendClaimRequest((Recommendation)9, "Notes"));

        result.ShouldHaveValidationErrorFor(x => x.Recommendation);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Recommend_EmptyNotes_HasError(string notes)
    {
        var result = _recommendValidator.TestValidate(new RecommendClaimRequest(Recommendation.Approve, notes));

        result.ShouldHaveValidationErrorFor(x => x.Notes);
    }

    [Fact]
    public void Recommend_NotesOver2000Characters_HasError()
    {
        var result = _recommendValidator.TestValidate(
            new RecommendClaimRequest(Recommendation.Approve, new string('n', 2001)));

        result.ShouldHaveValidationErrorFor(x => x.Notes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Decide_ApproveWithoutReason_HasNoErrors(string? reason)
    {
        var result = _decideValidator.TestValidate(new DecideClaimRequest(ClaimDecision.Approve, reason));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123456789")]          // 9 characters
    [InlineData("    short     ")]     // only 5 real characters once trimmed
    public void Decide_DenyWithoutAReasonOfTenCharacters_HasError(string? reason)
    {
        var result = _decideValidator.TestValidate(new DecideClaimRequest(ClaimDecision.Deny, reason));

        result.ShouldHaveValidationErrorFor(x => x.Reason); // BR-06
    }

    [Fact]
    public void Decide_DenyWithTenCharacterReason_HasNoErrors()
    {
        var result = _decideValidator.TestValidate(new DecideClaimRequest(ClaimDecision.Deny, "1234567890"));

        result.ShouldNotHaveAnyValidationErrors(); // BR-06 boundary
    }

    [Fact]
    public void Decide_MissingDecision_HasError()
    {
        var result = _decideValidator.TestValidate(new DecideClaimRequest(null, null));

        result.ShouldHaveValidationErrorFor(x => x.Decision);
    }

    [Fact]
    public void Decide_ReasonOver2000Characters_HasError()
    {
        var result = _decideValidator.TestValidate(
            new DecideClaimRequest(ClaimDecision.Approve, new string('r', 2001)));

        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }
}
