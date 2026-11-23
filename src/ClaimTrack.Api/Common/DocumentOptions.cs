namespace ClaimTrack.Api.Common;

// Settings from the "Documents" section of appsettings.json.
public class DocumentOptions
{
    public const string SectionName = "Documents";

    // Folder for uploaded files. Relative paths are inside the app's content root; the container
    // uses /data/documents (a volume). The API serves no static files, so nothing here is ever
    // reachable by URL: downloads only go through the permission-checked endpoint.
    public string StoragePath { get; set; } = "App_Data/documents";

    public long MaxSizeBytes { get; set; } = 10 * 1024 * 1024; // 10 MB

    public int MaxPerClaim { get; set; } = 20;

    public bool IsValid => !string.IsNullOrWhiteSpace(StoragePath) && MaxSizeBytes > 0 && MaxPerClaim > 0;
}
