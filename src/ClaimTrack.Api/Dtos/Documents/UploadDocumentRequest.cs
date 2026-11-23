namespace ClaimTrack.Api.Dtos.Documents;

// A multipart/form-data upload with one field, "file". Nullable so a missing file is a
// validation error (400) rather than a binding failure.
public record UploadDocumentRequest(IFormFile? File);
