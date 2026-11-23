using AwesomeAssertions;
using ClaimTrack.Api.Common;
using ClaimTrack.Api.Services;

namespace ClaimTrack.UnitTests.Services;

// The pure rules behind uploads: recognising files by their bytes, and cleaning file names.
public class DocumentFileRulesTests
{
    public static TheoryData<byte[], string?> Headers => new()
    {
        { "%PDF-1.7\n"u8.ToArray(), "application/pdf" },
        { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 }, "image/png" },
        { new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00 }, "image/jpeg" },
        { "MZ\u0090\u0000program"u8.ToArray(), null },           // a Windows program
        { "<html><script>"u8.ToArray(), null },                   // HTML
        { "<svg onload=alert(1)>"u8.ToArray(), null },            // SVG can carry scripts
        { "Just text pretending %PDF-"u8.ToArray(), null },       // the signature must be at the start
        { new byte[] { 0x25, 0x50 }, null },                      // too short to be anything
        { Array.Empty<byte>(), null }
    };

    [Theory]
    [MemberData(nameof(Headers))]
    public void Detect_FileHeader_RecognisesOnlyAllowedTypes(byte[] header, string? expected)
    {
        FileSignatures.Detect(header).Should().Be(expected);
    }

    [Fact]
    public async Task DetectAsync_LeavesTheStreamAtTheStart()
    {
        using var stream = new MemoryStream("%PDF-1.4 rest of the file"u8.ToArray());

        var type = await FileSignatures.DetectAsync(stream, CancellationToken.None);

        type.Should().Be("application/pdf");
        stream.Position.Should().Be(0, "the whole file still has to be saved afterwards");
    }

    [Theory]
    [InlineData("receipt.pdf", "receipt.pdf")]
    [InlineData("../../appsettings.json.pdf", "appsettings.json.pdf")]   // path traversal
    [InlineData(@"C:\Users\me\Desktop\photo.png", "photo.png")]          // Windows full path
    [InlineData("bad<>:\"|?*name.pdf", "badname.pdf")]                  // characters not allowed in names
    [InlineData("tab\there\u0000.pdf", "tabhere.pdf")]                   // control characters
    [InlineData("  .hidden.pdf.  ", "hidden.pdf")]                       // leading/trailing dots and spaces
    [InlineData("", "document.pdf")]                                     // nothing usable left
    [InlineData(null, "document.pdf")]
    [InlineData("../..", "document.pdf")]
    public void Sanitize_FileName_KeepsOnlyASafeDisplayName(string? fileName, string expected)
    {
        FileNames.Sanitize(fileName, "application/pdf").Should().Be(expected);
    }

    [Theory]
    [InlineData("image/png", "document.png")]
    [InlineData("image/jpeg", "document.jpg")]
    [InlineData("application/octet-stream", "document")]
    public void Sanitize_NoUsableName_UsesAnExtensionMatchingTheType(string contentType, string expected)
    {
        FileNames.Sanitize("", contentType).Should().Be(expected);
    }

    [Fact]
    public void Sanitize_VeryLongName_IsCutButKeepsItsExtension()
    {
        var name = new string('a', 400) + ".pdf";

        var result = FileNames.Sanitize(name, "application/pdf");

        result.Length.Should().Be(FileNames.MaxLength);
        result.Should().EndWith(".pdf");
    }

    [Fact]
    public void DocumentOptions_Defaults_Are10MbAnd20PerClaim()
    {
        var options = new DocumentOptions();

        (options.MaxSizeBytes, options.MaxPerClaim).Should().Be((10L * 1024 * 1024, 20));
        options.IsValid.Should().BeTrue();
        new DocumentOptions { MaxPerClaim = 0 }.IsValid.Should().BeFalse();
    }
}
