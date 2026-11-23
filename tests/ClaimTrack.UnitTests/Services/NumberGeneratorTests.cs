using AwesomeAssertions;
using ClaimTrack.Api.Services;

namespace ClaimTrack.UnitTests.Services;

// BR-10: the format of system-generated numbers. Uniqueness comes from the SQL Server
// sequence and is tested by the concurrent-requests integration test.
public class NumberGeneratorTests
{
    [Theory]
    [InlineData("POL", 2026, 42, "POL-2026-00042")]
    [InlineData("CLM", 2027, 1, "CLM-2027-00001")]
    [InlineData("CLM", 2026, 99_999, "CLM-2026-99999")]
    public void Format_PrefixYearAndValue_PadsToFiveDigits(string prefix, int year, int value, string expected)
    {
        NumberGenerator.Format(prefix, year, value).Should().Be(expected);
    }

    [Fact]
    public void Format_ValuePastFiveDigits_GrowsInsteadOfWrapping()
    {
        NumberGenerator.Format("POL", 2026, 123_456).Should().Be("POL-2026-123456");
    }
}
