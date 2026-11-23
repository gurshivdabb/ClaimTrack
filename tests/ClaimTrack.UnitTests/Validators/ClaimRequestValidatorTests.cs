using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Validators;
using FluentValidation.TestHelper;
using static ClaimTrack.UnitTests.TestClock;

namespace ClaimTrack.UnitTests.Validators;

// The shared rules are tested in ClaimDetailsValidatorTests. These tests check each
// request's own rules, and that the shared rules are included.
public class ClaimRequestValidatorTests
{
    private static readonly ClaimDetailsValidator ClaimDetailsValidator = new(CreateTimeProvider());

    private readonly SubmitClaimRequestValidator _submitValidator = new(ClaimDetailsValidator);
    private readonly UpdateClaimRequestValidator _updateValidator = new(ClaimDetailsValidator);

    private static SubmitClaimRequest ValidSubmitRequest() => new(
        PolicyId: Guid.NewGuid(),
        IncidentDate: Today,
        Description: "Burst pipe in the basement",
        ClaimedAmount: 4_000m);

    [Fact]
    public void SubmitValidate_ValidRequest_HasNoErrors()
    {
        var result = _submitValidator.TestValidate(ValidSubmitRequest());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void SubmitValidate_EmptyPolicyId_HasError()
    {
        var result = _submitValidator.TestValidate(ValidSubmitRequest() with { PolicyId = Guid.Empty });

        result.ShouldHaveValidationErrorFor(x => x.PolicyId);
    }

    [Fact]
    public void SubmitValidate_ZeroAmount_HasErrorFromSharedRules()
    {
        var result = _submitValidator.TestValidate(ValidSubmitRequest() with { ClaimedAmount = 0m });

        result.ShouldHaveValidationErrorFor(x => x.ClaimedAmount);
    }

    [Fact]
    public void UpdateValidate_FutureIncident_HasErrorFromSharedRules()
    {
        var request = new UpdateClaimRequest(Today.AddDays(1), "Burst pipe in the basement", 4_000m);

        var result = _updateValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.IncidentDate);
    }
}
