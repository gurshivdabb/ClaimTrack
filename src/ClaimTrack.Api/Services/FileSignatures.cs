namespace ClaimTrack.Api.Services;

// Identifies a file by its first bytes (its "signature" or "magic number"), never by its name or
// by the Content-Type the browser sends: both are chosen by the uploader and trivially faked.
// A program renamed to "photo.pdf" still starts with "MZ", so it's rejected.
public static class FileSignatures
{
    // The longest signature below; reading this many bytes is enough to recognise any allowed type.
    public const int HeaderLength = 8;

    private static readonly (string ContentType, byte[] Signature)[] Allowed =
    [
        ("application/pdf", "%PDF-"u8.ToArray()),
        ("image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        ("image/jpeg", [0xFF, 0xD8, 0xFF])
    ];

    public static string AllowedDescription => "PDF, PNG, or JPEG";

    // The content type for an allowed file, or null for anything else.
    public static string? Detect(ReadOnlySpan<byte> header)
    {
        foreach (var (contentType, signature) in Allowed)
        {
            if (header.StartsWith(signature))
            {
                return contentType;
            }
        }
        return null;
    }

    // Reads the first bytes of a stream and puts its position back, so it can still be saved in full.
    public static async Task<string?> DetectAsync(Stream content, CancellationToken cancellationToken)
    {
        var header = new byte[HeaderLength];
        var read = await content.ReadAtLeastAsync(header, HeaderLength, throwOnEndOfStream: false, cancellationToken);
        content.Position = 0;
        return Detect(header.AsSpan(0, read));
    }
}
