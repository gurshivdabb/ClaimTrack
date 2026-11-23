using AwesomeAssertions;
using ClaimTrack.Api.Common;

namespace ClaimTrack.UnitTests.Common;

public class RateLimitingOptionsTests
{
    [Fact]
    public void Defaults_AreFivePerMinuteForAuthAndAHundredForEverythingElse()
    {
        var options = new RateLimitingOptions();

        (options.Auth.PermitLimit, options.Auth.WindowSeconds).Should().Be((5, 60));
        (options.General.PermitLimit, options.General.WindowSeconds).Should().Be((100, 60));
        options.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(-1, 60)]
    [InlineData(5, 0)]
    public void IsValid_ZeroOrNegativeSetting_IsFalse(int permitLimit, int windowSeconds)
    {
        var options = new RateLimitingOptions { Auth = new RateLimitSettings { PermitLimit = permitLimit, WindowSeconds = windowSeconds } };

        options.IsValid.Should().BeFalse();
    }
}
