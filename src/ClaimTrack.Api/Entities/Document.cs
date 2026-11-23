namespace ClaimTrack.Api.Entities;

// A file attached to a claim (requirements §3). The bytes live in IDocumentStorage; this row holds
// the details and the storage key. Never updated or deleted: claim evidence is kept, like the audit log.
public class Document
{
    public Guid Id { get; set; }

    public Guid ClaimId { get; set; }
    public Claim Claim { get; set; } = null!;

    // The uploader's file name, cleaned, for display and downloads only. Never used as a path.
    public string FileName { get; set; } = string.Empty;

    // Detected from the file's own bytes, not taken from the upload.
    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    // The random key the file is stored under (see IDocumentStorage).
    public string StoragePath { get; set; } = string.Empty;

    public Guid UploadedById { get; set; }
    public User UploadedBy { get; set; } = null!;

    public DateTimeOffset UploadedAt { get; set; }
}
