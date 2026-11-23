using AwesomeAssertions;
using ClaimTrack.Api.Common;

namespace ClaimTrack.UnitTests.Common;

public class ResultTests
{
    [Fact]
    public void Ok_WithoutValue_IsSuccess()
    {
        var result = Result.Ok();

        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void NotFound_WithoutValue_IsNotSuccess()
    {
        var result = Result.NotFound();

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public void Conflict_WithoutValue_KeepsError()
    {
        var result = Result.Conflict("Policy has claims.");

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Conflict);
        result.Error.Should().Be("Policy has claims.");
    }

    [Fact]
    public void Ok_WithValue_KeepsValue()
    {
        var result = Result<int>.Ok(42);

        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().Be(42);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void NotFound_WithValueType_HasNoValue()
    {
        var result = Result<string>.NotFound();

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Value.Should().BeNull();
    }

    [Fact]
    public void Conflict_WithValueType_KeepsError()
    {
        var result = Result<string>.Conflict("Claim is under review.");

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Conflict);
        result.Error.Should().Be("Claim is under review.");
    }

    [Fact]
    public void Invalid_WithValueType_KeepsError()
    {
        var result = Result<string>.Invalid("Policy not found.");

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
        result.Error.Should().Be("Policy not found.");
    }

    [Fact]
    public void Unauthorized_WithValueType_KeepsError()
    {
        var result = Result<string>.Unauthorized("Invalid email or password.");

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Unauthorized);
        result.Error.Should().Be("Invalid email or password.");
    }
}
