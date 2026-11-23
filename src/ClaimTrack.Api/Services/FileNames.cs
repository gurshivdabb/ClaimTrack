namespace ClaimTrack.Api.Services;

// Cleans an uploaded file's name for display only. Files are always stored under a random name
// the server picks, so this name never touches the disk; it's still cleaned so it can't carry
// folder paths ("../../appsettings.json"), control characters, or an absurd length into the
// database, logs, or a download header.
public static class FileNames
{
    public const int MaxLength = 255;

    private static readonly char[] NotAllowed = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    public static string Sanitize(string? fileName, string contentType)
    {
        // Keep only the last part of any path, whichever separator the uploader's OS used.
        var name = (fileName ?? "").Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];

        name = new string(name.Where(c => !char.IsControl(c) && !NotAllowed.Contains(c)).ToArray());
        name = name.Trim().Trim('.').Trim();

        if (name.Length == 0)
        {
            name = "document" + DefaultExtension(contentType);
        }

        if (name.Length > MaxLength)
        {
            // Keep the extension, so "very long name … .pdf" still looks like a PDF.
            var extension = Path.GetExtension(name);
            name = name[..(MaxLength - extension.Length)] + extension;
        }

        return name;
    }

    private static string DefaultExtension(string contentType) => contentType switch
    {
        "application/pdf" => ".pdf",
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        _ => ""
    };
}
