using AwesomeAssertions;
using ClaimTrack.Api.Common;

namespace ClaimTrack.UnitTests.Common;

public class OverdueClaimsOptionsTests
{
    [Fact]
    public void Defaults_AreEnabledFourteenDaysHourly()
    {
        var options = new OverdueClaimsOptions();

        (options.Enabled, options.ThresholdDays, options.CheckIntervalMinutes).Should().Be((true, 14, 60));
        options.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(-1, 60)]
    [InlineData(14, 0)]
    public void IsValid_ZeroOrNegativeSetting_IsFalse(int thresholdDays, int checkIntervalMinutes)
    {
        new OverdueClaimsOptions { ThresholdDays = thresholdDays, CheckIntervalMinutes = checkIntervalMinutes }
            .IsValid.Should().BeFalse();
    }
}
