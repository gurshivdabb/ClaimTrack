using System.Text.Json;
using AwesomeAssertions;
using ClaimTrack.Api.Common;
using ClaimTrack.Api.Enums;

namespace ClaimTrack.UnitTests.Common;

public class JsonSettingsTests
{
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        JsonSettings.Apply(options);
        return options;
    }

    [Fact]
    public void Apply_SerializingEnum_WritesText()
    {
        var json = JsonSerializer.Serialize(ClaimStatus.UnderReview, CreateOptions());

        json.Should().Be("\"UnderReview\"");
    }

    [Fact]
    public void Apply_DeserializingEnumText_ReadsValue()
    {
        var value = JsonSerializer.Deserialize<CoverageType>("\"Home\"", CreateOptions());

        value.Should().Be(CoverageType.Home);
    }
}
